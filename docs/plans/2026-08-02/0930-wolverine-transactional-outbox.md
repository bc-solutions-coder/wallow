**status: active**

# Wolverine transactional outbox: binding writes to the messages they produce

Covers research item 1 of `0913-wolverine-severability-research.md` — the dual write.
Companion doc: `0922-wolverine-envelope-semantics.md` covers items 2–4 (handler isolation and
message identity). Neither doc restates the other; read the research doc for why severability
is the motivation and for the reference repositories cited below.

The two designs are independent and can land in either order. This one is larger.

---

## 1. The invariant

> **A database write and the messages it produces commit together, or neither happens.**

Nothing in Wallow enforces this today. Three defects follow from that one absence. They split
cleanly into **two populations that need two different fixes** — see §5, and §6 for the survey
that establishes the split.

---

## 2. Defect A — save, then publish (services)

The shape, everywhere:

```csharp
membership.Approve(roleId, actorId, timeProvider);
await memberships.SaveChangesAsync(ct);                       // transaction 1, commits

await messageBus.PublishAsync(new OrganizationMemberAddedEvent { ... });   // no transaction
```

`MembershipReviewService.ApproveAsync` — and 22 other sites. The window between the two lines
is small but real: a process restart, a Wolverine sending failure, or an exception raised
between them leaves the row committed and the event never sent. Nothing retries it, because
nothing recorded that it was owed.

`Program.cs` registers `opts.UseEntityFrameworkCoreTransactions()`, which reads as if this were
handled. It is inert: the transactional middleware only engages for a **handler** that Wolverine
can see holding a DbContext, and there is no `AutoApplyTransactions()`, no `[Transactional]`
attribute anywhere in the solution, and our handlers hold services rather than DbContexts.

**Inventory — `PublishAsync` sites in `api/src/Modules`** (51 total; SSE/Redis/SignalR excluded,
and `InvokeAsync` command/query mediation excluded because it is request/reply, not publishing):

| File | Sites |
| --- | --- |
| `Identity/…/Services/OrganizationService.cs` | 8 |
| `Identity/…/Services/UserManagementService.cs` | 4 |
| `Identity/…/Services/UserEnrollmentService.cs` | 3 |
| `Identity/…/Services/MembershipReviewService.cs` | 3 |
| `Identity/…/Services/PasswordlessService.cs` | 2 |
| `Identity/…/Services/InvitationService.cs` | 2 |
| `Identity/…/Services/SessionService.cs` | 1 |
| Wolverine handlers (already inside a message context) | 6 |
| Remaining single-site services across the other modules | 22 |

Identity holds 23 of the 51 and every one of the highest-count files.

Two sites deserve naming because they are worse than the general case:

- `OrganizationService.CreateAsync` calls `SaveChangesAsync` **three times** across two
  repositories and the DbContext before publishing `OrganizationCreatedEvent`. A failure
  part-way leaves a half-built organization with no event.
- `OrganizationService` line ~215 publishes `MembershipTransitionedEvent` without awaiting —
  fire-and-forget on top of the dual write.

## 3. Defect B — publish with no write at all (controllers)

A second population of publish sites sits in controllers that inject `IMessageBus` directly and
publish alongside a service call they do not share a transaction with. Same failure mode, one
layer up, and additionally it puts an infrastructure concern in the HTTP layer.

## 4. Defect C — domain events are raised and dropped

Discovered while taking the inventory for this doc. It is not in the research doc, and it is the
most serious of the three.

`AggregateRoot<TId>` collects raised events:

```csharp
private readonly List<IDomainEvent> _domainEvents = [];
public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();
protected void RaiseDomainEvent(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);
public void ClearDomainEvents() => _domainEvents.Clear();
```

Its XML comments describe infrastructure that does not exist — *"Cleared after persistence"*,
*"Events are dispatched after the aggregate is persisted"*, *"Called by infrastructure after
events are dispatched"*. There is no such infrastructure:

- `ClearDomainEvents()` has **no caller** in `api/src`.
- `DomainEvents` is read by **no** production code — every reference outside the base class is
  an EF `Ignore(...)` mapping.
- `TenantAwareDbContext` does not override `SaveChangesAsync`.
- `TenantSaveChangesInterceptor` is the only `SaveChangesInterceptor` in the solution, and it
  stamps tenant ids.
- Repositories delegate straight to `context.SaveChangesAsync`.

Raised domain events are therefore discarded when the DbContext goes out of scope. Consequences,
following the chain that `api/CLAUDE.md` documents as *"domain events raised in aggregates are
bridged to integration events in Application event handlers"*:

1. The three Inquiries domain-event handlers never execute — dead code.
2. So `InquirySubmittedEvent`, `InquiryStatusChangedEvent` and `InquiryCommentAddedEvent` are
   never published.
3. So the Notifications handlers subscribed to them never run: **no admin email, no in-app
   notification and no SSE push on inquiry submission, comment, or status change.**
4. The same holds for every other module's domain events (Storage file/bucket lifecycle,
   Notifications' own `NotificationCreated`/`NotificationRead`/`SmsSent`/push/email outcomes).

The suite is green because every domain-event handler is unit-tested by **direct invocation**
(`InquirySubmittedDomainEventHandler.HandleAsync(domainEvent, repository, …)`). Nothing asserts
that anything dispatches them. The wiring has no test because the wiring was never written.

`Diagnostics.DomainEventsPublishedTotal` is incremented by `WolverineModuleTaggingMiddleware`
for every message crossing Wolverine, so the metric named for this pipeline is measuring
something else and reads healthy.

---

## 5. Target design

Two mechanisms, each owning one population. They are not alternatives to weigh per-site — which
one applies is determined by where the code sits, and §6 shows the line falls cleanly.

| Population | Defect | Mechanism |
| --- | --- | --- |
| Inquiries, Storage, Notifications — 16 domain-event raise sites, **every one reached from a Wolverine handler** | C | **Cascading messages** |
| Identity — 23 publish sites in controller-driven services, **zero domain events** | A, B | **`IDbContextOutbox<T>`** |

### Mechanism 1 — cascading messages (fixes defect C)

The handler returns the messages it produces instead of publishing them through an injected bus.
Wolverine sends them as part of the same transaction:

> *"Cascading messages returned from handler methods will not be sent out until after the
> original message succeeds and is part of the underlying transport transaction."*

The current shape is what Wolverine's docs open the cascading page with, as the thing cascading
replaces — inject `IMessageContext`/`IMessageBus`, call `PublishAsync` inline. (Wolverine's
separate **Side Effects** feature is not used anywhere in Wallow.)

The aggregate method returns its event rather than raising it internally, and the handler relays
it. Both halves change:

```csharp
// before — Inquiry.Create raises internally, into a list nobody drains
public static Inquiry Create(...)
{
    Inquiry inquiry = new(...);
    inquiry.RaiseDomainEvent(new InquirySubmittedDomainEvent(...));
    return inquiry;
}

// after — the event is part of the method's contract
public static (Inquiry, InquirySubmittedDomainEvent) Create(...)
{
    Inquiry inquiry = new(...);
    return (inquiry, new InquirySubmittedDomainEvent(...));
}
```

This is the bulk of the work — 16 call sites across 9 aggregates — and it is what removes the
need for `RaiseDomainEvent` at all (§10). Where one method raises more than one event, return
`OutgoingMessages` instead of a tuple.

Every affected command handler returns `Task<Result<T>>` today. The conversion is uniform:

```csharp
// before
public static async Task<Result<InquiryDto>> HandleAsync(
    SubmitInquiryCommand command, IInquiryRepository repo, TimeProvider clock, CancellationToken ct)

// after
public static async Task<(Result<InquiryDto>, InquirySubmittedDomainEvent)> HandleAsync(
    SubmitInquiryCommand command, IInquiryRepository repo, TimeProvider clock, CancellationToken ct)
```

**The `Result<T>` return does not collide with cascading.** A tuple return normally treats *every*
member as a cascading message, which would wrongly publish `Result<InquiryDto>`. The
request/reply rule prevents it:

> *"in the case of using `InvokeAsync<T>()` for request/reply, the reply type of `T` **is not
> also published as a cascaded message**. Instead, it is only returned to the original caller."*

Controllers already call `InvokeAsync<Result<InquiryDto>>`. The requested reply type goes back to
the caller unpublished; every other tuple member cascades. This is the documented behaviour, not
a trick.

Two properties that matter beyond correctness:

- **Handlers become pure functions.** Their emitted messages are in the return value, so the
  direct-invocation unit tests we already have start *proving dispatch* instead of merely
  exercising handler bodies. That is precisely the blind spot that let defect C survive.
- **Cascaded messages get an independent retry loop** — *"handled separately in a later thread
  and with a completely independent 'retry loop' from the originating message."* See §7 for how
  this relates to `MultipleHandlerBehavior.Separated`.

Also converts: the three Inquiries domain-event handlers, which today inject `IMessageBus` to
publish their integration event. They return it instead, and shed the dependency.

### Mechanism 2 — `IDbContextOutbox<T>` (fixes defects A and B)

For Identity, whose logic lives in services called from controllers rather than from handlers:

```csharp
public async Task ApproveAsync(
    Guid organizationId, Guid userId, Guid actorId,
    IDbContextOutbox<IdentityDbContext> outbox, CancellationToken ct)
{
    membership.Approve(roleId, actorId, timeProvider);
    await outbox.PublishAsync(new OrganizationMemberAddedEvent { ... });
    await outbox.SaveChangesAndFlushMessagesAsync();   // one transaction, then flush
}
```

Wolverine's own guidance is that this exists because it had to, and that *"it's just always going
to be easiest to use Wolverine HTTP handlers or message handlers"*. It is the right answer for
Identity **as currently structured**; if Identity's controllers ever become Wolverine HTTP
endpoints, those sites should move to cascading and this mechanism should shrink toward zero.

### Rejected — EF Core domain-event harvesting

`opts.PublishDomainEventsFromEntityFrameworkCore<TEntity>(x => x.Events)` (Wolverine 5.6+, so
available on our 6.21.0) scrapes domain events off `DbContext.ChangeTracker` inside the
transaction. It was the leading candidate until the §6 survey, and is rejected because:

- **It buys nothing cascading doesn't**, given every raise site is reached from a handler.
- **It costs more:** a non-generic layer supertype above the generic `AggregateRoot<TId>`, a
  publicly drainable events collection, and a touch to every aggregate's EF `Ignore` mapping.
- **It is host-level config plus a shared-kernel base class**, both of which every extracted
  service would have to replicate. Cascading is self-contained in the handler.
- Wolverine's author endorses it only conditionally — *"**if** I were building a system that
  embeds domain event publishing directly in domain model entity classes, I would prefer this
  approach"* — while arguing the pattern itself *"makes application code harder to reason about
  and therefore more buggy over time."*

Keep it in mind only if a future raise site appears with no handler above it. A hand-rolled
`ISaveChangesInterceptor` beside `TenantSaveChangesInterceptor` is not a candidate at all.

### `AutoApplyTransactions()` — deliberately not adopted

The policy attaches transactional middleware to *"any handler that **depends on a `DbContext`
type**"* — a direct-dependency rule, not a transitive one. Our handlers depend on repositories and
services, so it would attach to nothing and change nothing. Adding it anyway would leave exactly
the false impression `UseEntityFrameworkCoreTransactions()` already gives (§2). Revisit only if
handlers start taking a DbContext directly.

One useful guarantee if that day comes: a handler depending on **more than one** DbContext type
*"fail[s] fast at startup"* rather than silently picking one.

### Designating the DbContext where there is more than one

Wallow has a DbContext per module, so several are in scope in principle:

| Attribute | Use |
| --- | --- |
| `[Transactional(typeof(IdentityDbContext))]` | Name the DbContext this handler's transaction spans |
| `[Storage(typeof(IdentityDbContext))]` | Name the storage for outbox enlistment |
| `[NonTransactional]` | Opt a handler out of an auto-transaction policy |

A handler that touches two module DbContexts is a module-boundary violation, not a designation
problem — the arch tests already forbid the project reference that would make it possible.

---

## 6. The survey that decides the split

Three findings, all verified against source. Together they are why mechanism 1 covers defect C
completely and why Identity is a separate problem rather than a straggler.

**1. Every `RaiseDomainEvent` site is shallow.** 16 sites across 9 aggregate files, each raise
sitting directly inside one aggregate method — a static factory (`Inquiry.Create`,
`StoredFile.Create`, `StorageBucket.Create`, `InquiryComment.Create`, `ChannelPreference.Create`)
or an instance method (`Inquiry.ChangeStatus`, `StoredFile.Delete`, `Notification.MarkRead`,
`SmsMessage`/`PushMessage`/`EmailMessage` sent-and-failed pairs). None is buried in a call chain
that would need a return value threaded up through intermediate methods.

**2. Every caller of an event-raising aggregate method is already a Wolverine handler.** Not one
is a controller-driven service:

| Module | Aggregates raising | Handlers at the entry point |
| --- | --- | --- |
| Inquiries | `Inquiry`, `InquiryComment` | `SubmitInquiryHandler`, `AddInquiryCommentHandler`, `UpdateInquiryStatusHandler` |
| Storage | `StoredFile`, `StorageBucket` | `CreateBucketHandler`, `DeleteBucketHandler`, `UploadFileHandler` |
| Notifications | `Notification`, `SmsMessage`, `PushMessage`, `EmailMessage`, `ChannelPreference` | `SendNotificationHandler`, `SendSmsHandler`, `SendPushHandler`, `DeliverPushHandler`, `SendEmailHandler`, `SetChannelEnabledHandler` |

Inquiries uses static handler classes; Storage and Notifications use sealed classes with
primary-constructor DI. Both are Wolverine handlers and both return `Task<Result<T>>`, so the
conversion in §5 is the same shape in all three modules.

**3. Identity raises zero domain events.** Its 23 publish sites construct and publish integration
events directly from services. Domain-event dispatch is irrelevant to Identity — it has only the
dual write.

---

## 7. What this does not change

- **Event shapes and aliases** — owned by `0922-wolverine-envelope-semantics.md`.
- **`MultipleHandlerBehavior.Separated`** — also that doc, and the interaction is worth stating
  precisely. Cascading gives each *cascaded message* its own retry loop; `Separated` gives each
  *handler of one message* its own. They solve adjacent halves and neither substitutes for the
  other: with `Separated` still unset, a reliably-delivered `InquirySubmittedEvent` fans into
  three handlers sharing one retry loop, so making delivery reliable **increases** how often the
  duplicate-email bug fires. **If only one design lands, land `Separated` first.**
- **The `IntegrationEvent` base record** — deferred there, still deferred here.
- **Error handling policies** — research item 6, unchanged.
- **Severability, mostly.** A cascaded message and a scraped one produce an identical envelope; a
  future standalone Notifications service cannot tell which produced the `InquirySubmittedEvent`
  it receives. The severability levers are the outbox itself, stable wire identity (`0922`), and
  the absence of cross-module FKs and sync calls — already true and arch-tested. Cascading helps
  only second-order, by making a module's outbound contract readable from handler signatures and
  by needing no host config or shared base class an extracted service would have to replicate.
  Choose it for testability and explicitness; the severability gain is a bonus, not the argument.

---

## 8. Migration order

Each step is independently shippable and independently testable.

| # | Step | Why here |
| --- | --- | --- |
| 1 | Convert Inquiries' 3 command handlers + 3 domain-event handlers to cascading | Smallest module, and the one whose broken chain is most visible |
| 2 | Assert the Inquiries → Notifications chain end to end | The regression test whose absence let defect C exist |
| 3 | Convert Storage's 3 handlers | Same shape, no new decisions |
| 4 | Convert Notifications' 6 handlers | Largest of the three, last because it is also the subscriber side |
| 5 | Identity's 23 sites → `IDbContextOutbox<IdentityDbContext>` | Independent of 1–4; can run in parallel |
| 6 | Controller publish sites → move the publish into the service it belongs to | Also removes `IMessageBus` from the HTTP layer |
| 7 | Arch tests: no `IMessageBus` in `*.Api` controllers; no `PublishAsync` outside a handler or an `IDbContextOutbox` scope | Keeps the pattern from regressing |

Step 2 is the one that matters most. Steps 1–4 and step 5 touch disjoint modules and can proceed
concurrently.

## 9. Testing

Direct handler invocation is what hid defect C. Under cascading it stops being the problem and
becomes part of the fix — the events are in the return value, so a direct call can assert them:

```csharp
(Result<InquiryDto> result, InquirySubmittedDomainEvent raised) =
    await SubmitInquiryHandler.HandleAsync(command, repo, clock, ct);

raised.ShouldNotBeNull();
```

That covers *what* a handler emits. It still says nothing about delivery, so the runtime
assertions remain:

- **Dispatch reaches the subscriber** — a tracked session:
  ```csharp
  await host.InvokeMessageAndWaitAsync(new SubmitInquiryCommand { ... });
  ```
- **The Inquiries → Notifications chain** — `TrackActivity().IncludeExternalTransports()
  .WaitForMessageToBeReceivedAt<T>()`, asserting the Notifications handler observed
  `InquirySubmittedEvent`. This is the step-2 regression test.
- **Atomicity** — force the send to fail after commit and assert the outbox row is present and
  retried, rather than the write standing alone.
- **Rollback** — force the write to fail and assert no message escapes.

## 10. Work breakdown

| Item | Scope | Risk |
| --- | --- | --- |
| Inquiries cascading | 3 command handlers, 3 domain-event handlers | Low — signature change plus dropping `IMessageBus` |
| Inquiries chain verification | New integration test; no production change | Low |
| Storage cascading | 3 handlers | Low |
| Notifications cascading | 6 handlers across 5 channels | Low, wide |
| Identity → `IDbContextOutbox<T>` | 7 services, 23 publish sites | Medium — largest diff, but per-service and independently shippable |
| Controller publish sites | Move publishes down a layer | Low, wide |
| Arch tests | Two assertions in `Wallow.Architecture.Tests` | None |

No change to `Wallow.Shared.Kernel.Domain`, no EF mapping churn, and no new `Program.cs`
configuration. `AggregateRoot<TId>`'s `RaiseDomainEvent`/`ClearDomainEvents` become unused once
the conversion is complete and should be deleted with it rather than left as scaffolding for a
dispatcher we decided not to build.
