**status: active**

# Wolverine severability research — can we extract a module into its own service?

Research question: is Wallow's Wolverine usage set up so that a large enough domain can later be
lifted out of the modular monolith and run as its own service behind a real broker, with the
change being mostly configuration rather than a rewrite?

Short answer: **the transport story is in good shape; the consistency story is not.** Swapping
local queues for RabbitMQ/Azure Service Bus really is close to a config change here. But events
are currently published outside the transaction that produced them, and three of the settings
Wolverine explicitly names for modular monoliths are unset. Both are cheap to fix now and
expensive to fix after a split.

---

## 1. Reference repositories worth reading

Ranked by how directly they answer *our* question.

### Tier 1 — read these

**[JasperFx/CritterStackSamples](https://github.com/JasperFx/CritterStackSamples)** — the highest-value
find. It contains the same e-commerce domain implemented twice:

- `EcommerceModularMonolith/` — one process, durable **local queues** between modules
- `EcommerceMicroservices/` — four services, same domain, **RabbitMQ** transport

Diffing those two directories is the closest thing that exists to a worked example of the exact
migration you're asking about. It shows what actually changes at the seam and what doesn't.

Also in that repo: `PaymentsMonolith/` (four modules, schema-per-module — our layout), and
`OutboxDemo/` (transactional outbox + saga, which is the pattern we're currently missing).

**[Wolverine — Modular Monoliths tutorial](https://wolverinefx.net/tutorials/modular-monolith.html)** —
the canonical guidance, and the source of the three configuration flags in §3. Its framing of
"severability" is the vocabulary for this whole exercise.

### Tier 2 — good architectural reference, different messaging stack

**[rafaelcaviquioli/modular-monolith-architecture](https://github.com/rafaelcaviquioli/modular-monolith-architecture)** —
.NET 10, Wolverine, **EF Core** (not Marten), explicitly "microservice-ready". Closest match to our
stack of anything public. Its module layout mirrors ours: contracts-only cross-module references,
DbContext-per-module, handlers discovered by convention. Its one idea we don't have is a per-module
facade interface (`IOrdersModule`) that internally does `bus.InvokeAsync` — so callers never see
command types, and the facade is the thing you swap for an HTTP/gRPC client on extraction.

**[kgrzybek/modular-monolith-with-ddd](https://github.com/kgrzybek/modular-monolith-with-ddd)** — the
reference modular monolith in .NET. Hand-rolled infrastructure, not Wolverine, so read it for
*boundary design and integration-event discipline*, not for messaging code.

**[devmentors/Inflow](https://github.com/devmentors/Inflow)** — Postgres + RabbitMQ modular monolith
with zero inter-module project references. Useful for seeing what "the broker is already there"
looks like structurally.

### Background reading

- [Wolverine 5 and Modular Monoliths](https://jeremydmiller.com/2025/10/27/wolverine-5-and-modular-monoliths/) —
  multiple message stores per process, `MessageStoreRole.Ancillary` + `.Enroll<TDbContext>()`.
  Relevant later: it's how you'd give one module its own database *before* extracting it.
- [Modular Monoliths and the Critter Stack](https://jeremydmiller.com/2024/04/15/modular-monoliths-and-the-critter-stack/) —
  the severability argument, and the warnings about over-messaging and anti-corruption-layer ceremony.

---

## 2. What we already have right

Worth stating plainly, because it's most of the hard part:

- **Modules communicate only through `Shared.Contracts` events**, enforced by
  `ModuleIsolationTests` — a real arch test, not a convention.
- **DbContext-per-module, schema-per-module.** All seven modules.
- **No cross-module foreign keys.** All seven `HasForeignKey` sites are intra-module
  (Identity→Identity, Inquiries→Inquiries, etc.). This is the #1 severability killer and we don't
  have it.
- **`opts.Durability.MessageStorageSchemaName = "wolverine"`** — matches the documented guidance.
- **Durable inbox/outbox enabled on all endpoints**, Postgres-backed.
- **`TenantStampingMiddleware` / `TenantRestoringMiddleware`** — stamping tenant onto outgoing
  envelopes and restoring it on incoming is *exactly* the pattern that survives a transport swap.
  In-process you could have cheated with an AsyncLocal; this design already works across a wire.
- **`WolverineAuthorizationMiddleware` validating tenant on external messages** — forward-looking,
  written for a world that has external messages.
- **`PublishAsync` everywhere, no explicit routing.** Boring is correct here: the call sites are
  transport-agnostic, so pointing them at a broker is a bootstrap change.

---

## 3. Gaps, ordered by how much they'd hurt after a split

### 3.1 Events are published outside the transaction that produced them — **critical**

This is the real finding. We have all the outbox *machinery* wired up and none of the outbox
*semantics*, because nothing ever enlists.

The publish sites are:

- **Controllers** injecting `IMessageBus` directly — `AccountController` alone has ~20
  `messageBus.PublishAsync` calls, plus `MfaController`, `SetupController`.
- **Infrastructure services** injecting `IMessageBus` — e.g.
  `MembershipReviewService.cs:79` does `await memberships.SaveChangesAsync(ct)` and then
  `:83` `await messageBus.PublishAsync(new OrganizationMemberAddedEvent { ... })`.

That second one is a textbook dual write. Two independent commits:

```
SaveChangesAsync()  ──commit──►  identity schema
PublishAsync()      ──commit──►  wolverine schema   ← separate transaction
```

Process dies between them → the membership exists and no one is ever told. Publish succeeds and
the commit rolls back → subscribers act on a membership that doesn't exist.

`UseDurableOutboxOnAllSendingEndpoints()` does **not** save us. It makes the *endpoint* durable —
the envelope is persisted before send — but on its own connection, not the one that saved the
aggregate. Durable ≠ atomic-with-your-write.

Confirming the middleware never engages: `UseEntityFrameworkCoreTransactions()` is called, but

- there is **no `opts.Policies.AutoApplyTransactions()`**, and
- there are **zero `[Transactional]` attributes** in `api/src`, and
- handlers don't take a `DbContext` anyway — they delegate to services that hold it
  (`CreateServiceAccountHandler` → `IServiceAccountService`).

So the transactional middleware has nothing to attach to. The EF Core integration is registered
and inert.

Today the blast radius is bounded: in-process, the window is milliseconds and a lost notification
email is survivable. **After extraction it isn't.** A lost `OrganizationMemberAddedEvent` that
crossed a broker to a separate Billing or Notifications service leaves two databases permanently
disagreeing, with no in-process transaction to reason about and no reconciliation path.

Two fixes, both idiomatic:

*Preferred — cascading messages from handlers.* Return the event from the handler and let
Wolverine's transactional middleware commit the DbContext and flush the envelope in one
transaction. Requires moving the publish out of the service and into the handler, and letting the
handler own the DbContext so `AutoApplyTransactions()` can see it.

*Where a controller or service genuinely must publish* — use the outbox explicitly:

```csharp
[HttpPost(...)]
public async Task Post(..., [FromServices] IDbContextOutbox<IdentityDbContext> outbox)
{
    outbox.DbContext.Memberships.Add(membership);
    await outbox.PublishAsync(new OrganizationMemberAddedEvent { ... });
    await outbox.SaveChangesAndFlushMessagesAsync();   // one transaction
}
```

Note the Wolverine docs' own comment on `IDbContextOutbox`: *"we had to do this feature, but it's
just always going to be easiest to use Wolverine HTTP handlers or message handlers."* The controller
form is the escape hatch, not the target.

One more, smaller: `MembershipReviewService.PublishTransitionAsync` (`:243`) returns the
`ValueTask` rather than awaiting inside a try — fine as written since callers await it, but worth
a look when this code moves.

### 3.2 `MultipleHandlerBehavior.Separated` is unset — **high**

Wolverine's default runs *all* handlers for a message type in **one** local queue, **one**
transaction, **one** retry loop.

We have several messages with multiple handlers — `InquirySubmittedEvent` is handled by both
`InquirySubmittedNotificationHandler` and `InquirySubmittedInAppHandler`. Today, if the in-app
handler throws, the retry re-executes the email handler too, and the DLQ entry covers the combined
execution.

After extraction those become two independently delivered messages with independent retries and
independent DLQ entries. **The failure semantics we test in the monolith are not the semantics we
get after the split.** Setting `Separated` now makes the monolith behave like the distributed
system from day one — which is the entire point of building a modular monolith first.

```csharp
opts.MultipleHandlerBehavior = MultipleHandlerBehavior.Separated;
```

### 3.3 `MessageIdentity.IdAndDestination` is unset — **high**

The default dedupes the inbox on envelope ID alone. Once the same event is delivered from a broker
to multiple subscribers sharing a message store, the second delivery is discarded as a duplicate —
a silently dropped message, the worst failure mode there is.

Named explicitly in the Wolverine modular-monolith guidance. It changes the dedup key, so it's a
behavioural change to persisted state — much better made now, pre-release, than later.

```csharp
opts.Durability.MessageIdentity = MessageIdentity.IdAndDestination;
```

### 3.4 Event contracts are bound to CLR type names — **high, and cheap**

Wolverine derives the wire type identifier from `typeof(T).FullName` unless told otherwise. Every
one of our ~40 integration events therefore has a wire contract of
`Wallow.Shared.Contracts.Identity.Events.OrganizationCreatedEvent`.

That means **renaming or moving a C# type is a breaking wire change** — which is fine today (one
process, one deploy) and is a production incident the day two services deploy independently. It
also means the extracted service's contract is a .NET namespace, which is an odd thing to hand to
a consumer.

Add stable identities now, while it costs nothing:

```csharp
[MessageIdentity("identity.organization-created", Version = 1)]
public sealed record OrganizationCreatedEvent : IntegrationEvent { ... }
```

Wolverine supports versioned evolution from there via `IForwardsTo<T>` plus
`opts.RegisterMessageForwarder<PersonBorn, PersonBornV2>()` (explicit registration is required as
of Wolverine 6). This is the single highest value-per-effort item on the list — an arch test can
enforce that every `IIntegrationEvent` carries the attribute.

### 3.5 `Shared.Contracts` mixes messages with synchronous in-process interfaces — **high**

This is the biggest *architectural* severability risk, and it isn't a Wolverine setting at all.

`Wallow.Shared.Contracts` holds our integration events, which is right. It also holds:

`IUserService`, `IUserQueryService`, `IScopeSubsetValidator`, `ISetupStatusProvider`,
`IStorageProvider`, `IApiKeyService`, `ISseDispatcher`, `IRealtimeDispatcher`,
`IPresenceService`, `IRealtimeAccessRevoker`, `IEmailService`

Every one of those is a **synchronous in-process call that becomes a network hop on extraction**,
and none of them are messages. They pass the `ModuleIsolationTests` check because the test only
forbids `Wallow.{OtherModule}` references — a shared-contracts interface is invisible to it.

They also make the contracts assembly non-severable: extracting Notifications means taking a
reference on an assembly that carries Storage's and Identity's contracts too.

Suggested direction (not urgent, but decide before the first extraction):

- Split `Shared.Contracts` per module — `Wallow.Contracts.Identity`, `Wallow.Contracts.Storage`, … —
  so an extracted service references only what it consumes.
- Separate **messages** from **service interfaces** and treat the latter as a known list of future
  network calls. For each, decide now: does it become a message, an HTTP call, or does it prove
  those two modules are actually one bounded context? (Jeremy Miller's test: *"if two modules are
  chatty and frequently change together, they're one bounded context."*)
- Consider the `rafaelcaviquioli` facade pattern — a single `I{Module}Module` interface per module
  that internally does `bus.InvokeAsync`, so there's exactly one type to replace with a client.

### 3.6 Error handling is monolith-shaped — **medium**

```csharp
opts.Policies.OnException<InvalidOperationException>().RetryTimes(2).Then.MoveToErrorQueue();
opts.Policies.OnAnyException().RetryTimes(1).Then.MoveToErrorQueue();
```

One in-memory retry then DLQ is reasonable for local queues. Across a broker it isn't:

- No policy for **transient infrastructure faults** — `NpgsqlException` with `IsTransient`, broker
  connection drops, socket timeouts. These need `ScheduleRetry` with real backoff (seconds to
  minutes), not one immediate retry.
- `InvalidOperationException` gets *more* retries than anything else, but it's almost always a
  logic bug — it should fail fast to the DLQ, not burn two attempts.
- Policies are global. Per-message-type policies matter more once a poison message can block a
  partition.

Not urgent, but revisit as part of the same change that introduces a broker.

### 3.7 Smaller items

- **`IntegrationEvent` base uses `DateTime.UtcNow` and `DateTime`, not `TimeProvider`/`DateTimeOffset`.**
  We inject `TimeProvider` elsewhere (`MembershipReviewService`), so this is inconsistent and makes
  event timing untestable. `DateTimeOffset` is also the safer cross-service wire type.
- **`opts.Policies.LogMessageStarting(LogLevel.Debug)`** is fine, but there's no
  `IncludeExternalTransports()` test coverage. Wolverine's tracked sessions
  (`host.TrackActivity().IncludeExternalTransports().WaitForMessageToBeReceivedAt<T>(host)`) are how
  you'd prove a cross-service flow end to end. Worth adopting in integration tests *before* the split
  so the tests survive it.
- **No arch test asserting transport-agnosticism** — nothing stops someone adding a
  `LocalQueue`-specific assumption or an `InvokeAsync` across a module boundary. The Wolverine docs
  specifically discourage cross-module `InvokeAsync`; today `SetupController` uses it, which is fine
  (same module), but there's no guard.

---

## 4. Recommended order of work

Everything here is pre-release, so no compatibility shims are needed — reshape and re-seed.

1. **`MultipleHandlerBehavior.Separated` + `MessageIdentity.IdAndDestination`** — two lines in
   `Program.cs`. Do these first; `Separated` will surface any handler that was implicitly relying on
   sharing a transaction with a sibling handler, and you want to find those now.
2. **`[MessageIdentity]` on every integration event**, plus an arch test enforcing it. Mechanical,
   ~40 files, permanently removes the "renaming a class broke production" class of bug.
3. **Fix the dual write.** The largest change. Move publishing into handlers with
   `AutoApplyTransactions()`, use `IDbContextOutbox<T>` where a controller must publish directly.
   Start with Identity — it has the most publish sites and the most consequential events.
4. **Split `Shared.Contracts` per module**, and inventory the synchronous service interfaces into
   "becomes a message" / "becomes an HTTP call" / "these two modules are one bounded context".
5. **Tighten error handling** and adopt tracked-session integration tests with
   `IncludeExternalTransports()`.
6. **Prove it.** Extract one small module (Inquiries is the obvious candidate — few events, no
   inbound service interfaces) onto RabbitMQ *in a spike branch*. Time-box it. Whatever breaks is
   the real gap list, and it'll be shorter than anything written here.

Step 6 is worth more than steps 1–5 combined as a source of truth. But steps 1–3 are cheap and
should land regardless, because they're each harder to do later than now.
