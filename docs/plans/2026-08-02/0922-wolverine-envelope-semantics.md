**status: active**

# Wolverine envelope semantics: handler isolation and message identity

Design for research items 2–4 of `0913-wolverine-severability-research.md`. That document carries
the background, the reference repos, and the full gap list; this one is the implementation design
for three of its findings and does not restate them.

Companion doc: `0930-wolverine-transactional-outbox.md` covers research item 1 (the dual write).
The two are independent and can land in either order, though this one is cheaper and should go
first — it surfaces coupling that the outbox work would otherwise have to reason about blind.

---

## Problem

Three defaults in our Wolverine configuration are correct for a single-process application and
wrong for one that intends to shed a module into its own service. Each is cheap to change now and
progressively more expensive later:

| # | Default | Consequence after extraction |
| --- | --- | --- |
| A | All handlers for a message share one queue, transaction and retry loop | Failure semantics tested in the monolith are not the ones the split produces |
| B | Inbox dedupes on envelope id alone | Fan-out to multiple listeners silently drops all but the first delivery |
| C | Wire type id is `typeof(T).FullName` | Renaming or moving a C# type is a breaking wire change |

A and B are two lines in `Program.cs`. C is a mechanical pass over 37 files plus one arch test.

**A and B are not independent** — see §3.

## Goals

- The monolith's per-handler failure, retry and dead-letter behaviour matches what a broker-backed
  split would produce, so tests written now stay meaningful after extraction.
- Every integration event has a wire identity that survives C# refactoring, including the
  per-module split of `Shared.Contracts` contemplated in the research doc.
- The above is enforced by tests, not convention.

## Non-goals

- Introducing an external broker. Nothing here requires one; all three changes are about making
  local behaviour honest.
- Fixing the dual write (separate doc).
- Splitting `Shared.Contracts` per module. Change C is a *precondition* for doing that safely
  later, because it decouples the wire contract from the namespace — but the split itself is out
  of scope.
- Changing the `IntegrationEvent` base record. See §6.

---

## 1. Change A — `MultipleHandlerBehavior.Separated`

```csharp
opts.MultipleHandlerBehavior = MultipleHandlerBehavior.Separated;
```

### Current state

Four events have more than one handler:

| Event | Handlers | Owning modules |
| --- | --- | --- |
| `InquirySubmittedEvent` | 3 — `InquirySubmittedInAppHandler`, `InquirySubmittedNotificationHandler`, `InquirySubmittedSseHandler` | Notifications |
| `InquiryCommentAddedEvent` | 3 — `InApp`, `Notification`, `Sse` | Notifications |
| `InquiryStatusChangedEvent` | 2 — `Notification`, `Sse` | Notifications |
| `EmailVerifiedEvent` | 2 — `EmailVerifiedInquiryLinkHandler`, `EmailVerifiedNotificationHandler` | **Inquiries + Notifications** |

Under the default (`MultipleHandlerBehavior.Aggregated`) each group executes as a single unit: one
local queue, one transaction, one retry loop, one dead-letter entry.

### Why this is a present bug, not only a future one

`InquirySubmittedEvent`'s three handlers have genuinely different reliability requirements:

- `InquirySubmittedSseHandler` calls `ISseDispatcher.SendToTenantAsync` — a Redis publish. It is
  **not meaningfully retryable**: a realtime push replayed a minute later is stale by definition.
- `InquirySubmittedInAppHandler` writes persistent notifications for each admin. **Must** be
  retried on failure.
- `InquirySubmittedNotificationHandler` sends email. Must be retried, and must not double-send.

Today a Redis blip in the SSE handler throws, and our `OnAnyException().RetryTimes(1)` policy
re-executes **all three** — so a transient realtime failure produces a duplicate admin email and
duplicate in-app notifications.

`EmailVerifiedEvent` is the worse case because it crosses a module boundary.
`EmailVerifiedInquiryLinkHandler` (Inquiries) writes to the Inquiries database;
`EmailVerifiedNotificationHandler` (Notifications) sends the welcome email. A transient Inquiries
write failure re-sends the welcome email. Two modules share one failure domain — exactly the
coupling the module boundary exists to prevent, reintroduced by a messaging default.

### What `Separated` buys

Each handler gets its own local queue, and therefore its own transaction, retry policy, dead-letter
entry, durability mode and trace span. That is the granularity a broker-backed split imposes
anyway; adopting it now means the failure modes we test are the ones we ship.

It also makes the mixed-durability requirement above expressible, which it currently is not.
Marking the SSE queues buffered is a natural follow-on and is listed in §7 as optional.

### What changes observably

- **Dead-letter granularity.** A failure now dead-letters one handler's envelope rather than the
  message. Any test asserting DLQ row counts for a multi-handler event needs updating.
- **Retry counts.** Three handlers with `RetryTimes(1)` can now produce three independent retries
  rather than one shared one.
- **Ordering.** Handlers previously ran sequentially within one execution; they now run
  independently and may interleave. Verified: none of the eight handlers above reads state written
  by a sibling, so no ordering dependency exists to break. This must be re-verified if the list
  grows.

---

## 2. Change B — `MessageIdentity.IdAndDestination`

```csharp
opts.Durability.MessageIdentity = MessageIdentity.IdAndDestination;
```

### What it does

It widens the primary key of the incoming-envelope table from `id` to `(id, destination)`.

Wolverine's inbox deduplication works by attempting the insert and treating a **primary key
violation** as "already handled". With the default `MessageIdentity.Id`, an envelope id can appear
in the inbox exactly once, whatever its destination.

### Why the default is wrong for us

When the same message is delivered to more than one listener backed by the same message store, the
second delivery collides on the PK and is discarded as a duplicate. Because the mechanism *is* the
PK violation, the outcome is **a silently dropped message** — no exception surfaces, nothing reaches
the dead-letter queue, and the only symptom is a handler that never ran.

Wolverine's modular-monolith guidance names this setting for exactly this reason.

### Cost

This is a schema change to `wolverine.wolverine_incoming_envelopes`. Wolverine provisions its own
schema at startup; we do not manage it through `Wallow.MigrationService`.

Per the deployment-status section of the root `CLAUDE.md` — pre-release, no consumers, disposable
local databases — the migration is: **drop the `wolverine` schema and let Wolverine rebuild it.**
No expand/contract, no dual-write window. This applies to local dev databases and to the E2E stack
in `docker/docker-compose.test.yml`, which is recreated per run anyway.

---

## 3. Why A and B must land together

They interact, and the interaction is the whole point.

`Separated` means one published event yields *N* deliveries rather than one. In-process with local
queues those deliveries are distinct envelopes and the default identity holds. Once an external
broker fans the same envelope out to *N* listeners sharing one message store, they share an
envelope id — and under `MessageIdentity.Id`, *N−1* of them are silently swallowed as duplicates.

Landing A alone is safe today and becomes a silent-message-loss bug the day a broker appears.
Landing B alone is harmless but pointless. Ship them in one commit.

---

## 4. Change C — stable wire identities

### Current state

37 integration event records in `Wallow.Shared.Contracts`:

| Namespace | Count |
| --- | --- |
| `Identity/Events` | 29 |
| `Delivery/Events` | 3 |
| `Inquiries/Events` | 3 |
| `Announcements/Events` | 1 |
| `Notifications/Events` | 1 |

None carries `[MessageIdentity]`, so each one's wire identity is its fully-qualified CLR name —
`Wallow.Shared.Contracts.Identity.Events.OrganizationCreatedEvent`.

### Three problems with that

1. Renaming or moving a C# type is a breaking wire change. Harmless in one process and one deploy;
   a production incident once two services deploy independently.
2. The research doc proposes splitting `Shared.Contracts` per module. Under CLR-name identity that
   split changes **every namespace and therefore every wire contract at once** — which is fine
   pre-release, and impossible afterwards. Aliasing first makes the split a no-op on the wire.
3. A .NET namespace is a poor contract to publish to a non-.NET consumer.

### Design

Apply `[MessageIdentity]` to every concrete `IIntegrationEvent`:

```csharp
[MessageIdentity("identity.organization-created", Version = 1)]
public sealed record OrganizationCreatedEvent : IntegrationEvent
{
    public required Guid OrganizationId { get; init; }
    // ...
}
```

**Alias scheme:** `{owner}.{event}`, both segments kebab-case, derived from the event's own name
rather than its namespace.

| Event | Alias |
| --- | --- |
| `OrganizationCreatedEvent` | `identity.organization-created` |
| `UserRegisteredEvent` | `identity.user-registered` |
| `InquirySubmittedEvent` | `inquiries.inquiry-submitted` |
| `AnnouncementPublishedEvent` | `announcements.announcement-published` |
| `NotificationCreatedEvent` | `notifications.notification-created` |
| `EmailSentEvent` | `delivery.email-sent` |

The trailing `Event` suffix is dropped — it carries no information the message type doesn't already
imply.

**Open decision — the `Delivery` prefix.** `Delivery/Events` (`EmailSentEvent`, `SmsSentEvent`,
`PushSentEvent`) has no owning module; the events are produced by Notifications' outbound channels.
Two options: fold them under `notifications.` for a one-to-one owner mapping, or keep `delivery.`
as a deliberate contract family for send-receipt events that a future Delivery service would own.
Recommendation: **keep `delivery.`** — the receipts are plausibly a separate service's output, and
a prefix costs nothing to keep and a wire break to introduce later.

**Versioning.** All events start at `Version = 1`. Breaking evolution uses `IForwardsTo<TNew>`
plus explicit registration, which Wolverine 6 requires (we are on 6.21.0):

```csharp
opts.RegisterMessageForwarder<OrganizationCreatedEvent, OrganizationCreatedEventV2>();
```

No forwarders are needed now. Documenting the path is the point.

### Enforcement

One new arch test class in `Wallow.Architecture.Tests`, following the existing
`SharedContractsTests` / `WolverineConventionTests` shape (NetArchTest + reflection over
`typeof(UserRegisteredEvent).Assembly`). Four assertions:

1. Every concrete type implementing `IIntegrationEvent` carries `[MessageIdentity]`.
2. Aliases are unique across the assembly.
3. Each alias matches `^[a-z0-9]+(-[a-z0-9]+)*\.[a-z0-9]+(-[a-z0-9]+)*$`.
4. Each alias prefix is a known owner — `TestConstants.AllModules` lowercased, plus `delivery`.

Assertion 4 is the one that turns the naming scheme into a contract rather than a suggestion. It
must land in the same commit as the attributes; a partial application is functionally harmless but
leaves an inconsistent public surface with nothing to catch the next omission.

---

## 5. Rollout

Two commits.

**Commit 1 — `feat(api)!: isolate wolverine handlers and widen inbox identity`**

- `opts.MultipleHandlerBehavior = MultipleHandlerBehavior.Separated;`
- `opts.Durability.MessageIdentity = MessageIdentity.IdAndDestination;`
- Drop the local `wolverine` schema; confirm rebuild on next start.
- Fix any integration tests asserting aggregated retry/DLQ behaviour.

**Commit 2 — `feat(api)!: give integration events stable wire identities`**

- `[MessageIdentity]` on all 37 events.
- New arch test class.

Both are breaking in the conventional-commit sense (wire behaviour and wire contract), which is
correct and costs nothing pre-release.

## 6. Explicitly deferred

`IntegrationEvent` base record uses `DateTime.UtcNow` and exposes `OccurredAt` as `DateTime`
(research §3.7). We inject `TimeProvider` elsewhere — `MembershipReviewService` does — so this is
inconsistent and makes event timing untestable, and `DateTimeOffset` is the safer cross-service
wire type.

It is tempting to fold this in since commit 2 touches all 37 files anyway. **Don't.** Changing the
property type ripples into every publish site and every handler that reads `OccurredAt`, which is a
different and larger change than adding an attribute. File it separately.

## 7. Optional follow-on, unlocked by Change A

With `Separated`, per-handler queue configuration becomes possible. The SSE handlers
(`InquirySubmittedSseHandler`, `InquiryCommentAddedSseHandler`, `InquiryStatusChangedSseHandler`)
should arguably run on **buffered, non-durable** queues with no retry: a replayed realtime push is
worse than a dropped one. This is a genuine behaviour decision rather than a mechanical change, so
it is listed here rather than in §5.

## 8. Testing

Beyond keeping the existing suite green, one new test earns its place: a Wolverine tracked-session
test asserting that `InquirySubmittedEvent` produces **three independent handler executions**, and
that forcing the SSE handler to throw does **not** re-execute the email handler.

That is the test that would have caught the duplicate-email bug in §1, and it is the test that will
still mean the same thing after Notifications becomes its own service.

## 9. Work breakdown

| # | Task | Scope |
| --- | --- | --- |
| 1 | Set `Separated` + `IdAndDestination`; drop and rebuild the `wolverine` schema | `Wallow.Api/Program.cs` |
| 2 | Repair tests asserting aggregated retry/DLQ behaviour | Identity integration tests, module tests |
| 3 | Tracked-session test for independent handler execution | `Wallow.Identity.IntegrationTests` or Notifications tests |
| 4 | Apply `[MessageIdentity]` to 37 events | `Wallow.Shared.Contracts` |
| 5 | Arch test enforcing presence, uniqueness, format and known-owner prefix | `Wallow.Architecture.Tests` |
| 6 | *(optional)* Buffered non-durable queues for the three SSE handlers | `Program.cs` + Notifications |

Tasks 1–3 are one unit of work; 4–5 are another. Task 6 is independent and needs a decision first.
