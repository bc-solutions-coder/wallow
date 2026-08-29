# Messaging

This guide covers how Wallow's modules talk to each other. Everything runs on **Wolverine**, used as
a single unified CQRS mediator and message bus inside one process — there is no external broker.

## Overview

Wallow is a modular monolith. Modules (Identity, Storage, Notifications, Announcements, Inquiries,
ApiKeys, Branding) never reference each other's projects. The only cross-module coupling allowed is a
project reference to `Wallow.Shared.Contracts`. That assembly is the public boundary between modules
and holds three kinds of thing: the integration event records, the cross-module service and query
interfaces (`IUserService`, `IApiKeyService`, `IStorageProvider`, `ISseDispatcher`, and others), and
the supporting value types those signatures need. It also holds exactly one command —
`Storage/Commands/UploadFileCommand.cs` — because Storage's upload is invoked from another module.
That is the exception, not the pattern: a command normally stays inside its own module.

Three kinds of message travel over the bus:

| Kind | How it is sent | Scope |
|------|----------------|-------|
| Commands and queries | `IMessageBus.InvokeAsync(...)` — runs the handler inline and returns its result | Within one module, except `UploadFileCommand` |
| Integration events | `IMessageBus.PublishAsync(...)` — fans out to every discovered handler | Across modules |
| Domain events | `AggregateRoot.RaiseDomainEvent(...)` collects them on the aggregate | Within one module |

Domain events (`IDomainEvent`, `api/src/Shared/Wallow.Shared.Kernel/Domain/IDomainEvent.cs`) are an
internal implementation detail of a module. Integration events (`IIntegrationEvent`,
`api/src/Shared/Wallow.Shared.Contracts/IIntegrationEvent.cs`) are the published contract — once
another module handles one, changing its shape is a breaking change.

## Integration Events

An integration event is an immutable record in `api/src/Shared/Wallow.Shared.Contracts/` that inherits
the `IntegrationEvent` base record, which supplies `EventId` (`Guid`) and `OccurredAt` (`DateTime`).

```csharp
namespace Wallow.Shared.Contracts.Identity.Events;

/// <summary>
/// Published when a user requests email verification.
/// </summary>
public sealed record EmailVerificationRequestedEvent : IntegrationEvent
{
    public required Guid UserId { get; init; }
    public required Guid TenantId { get; init; }
    public required string Email { get; init; }
    public required string FirstName { get; init; }
    public required string VerifyUrl { get; init; }
}
```

Authoring rules, taken from the conventions the existing events already follow:

- **Namespace** — `Wallow.Shared.Contracts.{Module}.Events`, one file per event. The namespace is what
  the AsyncAPI generator uses to attribute the event to a module, so it is load-bearing, not cosmetic.
- **Past-tense naming** — `EmailVerifiedEvent`, not `VerifyEmailEvent`. Events state a fact that already
  happened; they are not instructions.
- **Primitives only** — `Guid`, `string`, `DateTime`, `decimal` and simple DTOs. Never domain entities or
  strongly-typed IDs, because events are serialized into the message store.
- **Carry the context handlers need** — `TenantId`, `UserId`, and the entity ID. A handler in another
  module cannot reach back into your schema to look things up.
- **Immutable records** — `sealed record` with `required` / `init` properties.

The event directories that exist today:

| Directory | Events |
|-----------|--------|
| `Wallow.Shared.Contracts/Identity/Events/` | Registration, login, MFA, password, email-change, organization and session events |
| `Wallow.Shared.Contracts/Inquiries/Events/` | `InquirySubmittedEvent`, `InquiryStatusChangedEvent`, `InquiryCommentAddedEvent` |
| `Wallow.Shared.Contracts/Announcements/Events/` | `AnnouncementPublishedEvent` |
| `Wallow.Shared.Contracts/Notifications/Events/` | `NotificationCreatedEvent` |
| `Wallow.Shared.Contracts/Delivery/Events/` | `EmailSentEvent`, `SmsSentEvent`, `PushSentEvent` |

`Wallow.Shared.Contracts` has zero package dependencies by design — keep it that way.

## Handlers

A handler is a plain class with a `Handle` or `HandleAsync` method whose first parameter is the message
type. There is no per-handler registration step, but the *assemblies* are declared rather than
scanned: each module lists its own in `IWallowModule.HandlerAssemblies`
(`api/src/Modules/{Module}/*.Infrastructure/Modules/{Module}Module.cs`), `WallowModuleRegistry.All`
collects them, and `WallowModules.AddWallowModules` filters that list down to the modules the host
enabled. `api/src/Wallow.Api/Program.cs` hands exactly those assemblies to
`opts.Discovery.IncludeAssembly`, plus the host assembly and `Wallow.Shared.Infrastructure`, which
owns handlers but belongs to no module.

Because the list is per-module and filtered, **a disabled module's handlers are not discovered at
all** — the messages it used to consume simply go unhandled. That is a real behaviour change from the
old scan-every-loaded-`Wallow.*`-assembly mechanism, which found handlers whether or not their module
had been registered. Since every module declares both its `.Application` and its `.Infrastructure`
assembly unconditionally, a new handler in either layer of an *enabled* module still needs nothing
written anywhere.

Dependencies are injected as **method parameters**, not constructor parameters, and handler classes
are usually `static`.

Publishing an event does not require the publisher to know who consumes it. Several modules can handle
the same event independently: `EmailVerifiedEvent` is consumed both by
`Wallow.Notifications.Application/EventHandlers/EmailVerifiedNotificationHandler.cs` and by
`Wallow.Inquiries.Application/EventHandlers/EmailVerifiedInquiryLinkHandler.cs`, which back-links
inquiries that were submitted anonymously before the user verified that address.

## End-to-End Example

Following `EmailVerificationRequestedEvent` from publish to sent email:

**1. Contract** — `api/src/Shared/Wallow.Shared.Contracts/Identity/Events/EmailVerificationRequestedEvent.cs`
declares the record shown above.

**2. Publisher (Identity)** —
the `Register` action in
`api/src/Modules/Identity/Wallow.Identity.Api/Controllers/AccountController.cs` generates the
confirmation token, builds the verify URL, and publishes:

```csharp
await messageBus.PublishAsync(new EmailVerificationRequestedEvent
{
    UserId = user.Id,
    TenantId = user.TenantId,
    Email = user.Email!,
    FirstName = user.FirstName,
    VerifyUrl = verifyUrl
});
```

`messageBus` is Wolverine's `IMessageBus`, injected into the controller's primary constructor. The same
event is published from `CompleteExternalRegistration` in that controller for users arriving
through an external identity provider. Identity does not know that Notifications exists.

**3. Consumer (Notifications)** —
`api/src/Modules/Notifications/Wallow.Notifications.Application/EventHandlers/EmailVerificationNotificationHandler.cs`
renders the `emailverification` template and hands off to the module's own send-email command:

```csharp
public static class EmailVerificationNotificationHandler
{
    public static async Task Handle(
        EmailVerificationRequestedEvent message,
        IEmailTemplateService templateService,
        IMessageBus bus)
    {
        string body = await templateService.RenderAsync("emailverification", new
        {
            message.FirstName,
            message.VerifyUrl
        });

        SendEmailCommand emailCommand = new(
            To: message.Email,
            From: null,
            Subject: "Verify your email address",
            Body: body);

        await bus.InvokeAsync(emailCommand);
    }
}
```

**4. Delivery** — `SendEmailCommand` is a same-module command handled by
`Wallow.Notifications.Application/Channels/Email/Commands/SendEmail/SendEmailHandler.cs`. Because it is
sent with `InvokeAsync`, it executes inline and its result (or exception) comes back to the caller.

Note the asymmetry: `PublishAsync` for the cross-module event, `InvokeAsync` for the intra-module
command. That is the pattern to copy.

## Transport and Durability

Wolverine is configured in `api/src/Wallow.Api/Program.cs` (the `builder.Host.UseWolverine(...)` block).
There are no broker transport packages in the build — `api/Directory.Packages.props` references only
`WolverineFx`, `WolverineFx.RuntimeCompilation`, `WolverineFx.FluentValidation`,
`WolverineFx.EntityFrameworkCore`, and `WolverineFx.Postgresql`. Messages are therefore delivered
**in-process, through Wolverine's local queues**. What this means operationally:

- **No broker to run or monitor.** Nothing to provision, and no network hop between publisher and
  handler.
- **No cross-process delivery.** An external service cannot subscribe to a Wallow event, and a second
  API instance does not see events published by the first. Everything happens inside the API process.
- **Handlers still run asynchronously.** `PublishAsync` queues the envelope and returns; the handler runs
  on a background thread. Do not rely on a handler's side effects being visible when the HTTP request
  that published the event returns.

Durability is not left in memory, though. Program.cs calls `PersistMessagesWithPostgresql(...)`
against the `DefaultConnection` database with
`opts.Durability.MessageStorageSchemaName = "wolverine"` in **every** environment, Testing
included — a host without a message store cannot execute a transactional handler chain at all
(`EfCoreEnvelopeTransaction` throws on the first one), and leaving Testing without a store would
make the transactional path the only path production takes but no test does. Outside the
`Testing` environment it then enables `UseDurableInboxOnAllListeners()` and
`UseDurableOutboxOnAllSendingEndpoints()`. Concretely:

| Behaviour | Effect |
|-----------|--------|
| Durable inbox | Envelopes are written to the `wolverine` Postgres schema before handling, giving at-least-once delivery with deduplication across a restart |
| Durable outbox | Messages are only released after the enclosing transaction commits, so a rolled-back request never emits an event |
| EF Core integration | `UseEntityFrameworkCoreTransactions()` enlists outgoing messages in the module's EF Core transaction |
| `Testing` environment | Postgres persistence still registers — Wolverine auto-migrates its `wolverine` schema into the test container on startup. `opts.Durability.Mode = DurabilityMode.Solo` skips leadership election, since every test class boots its own single-node host; only the durable inbox/outbox policies are skipped |

Because handlers are at-least-once, **write them idempotently** — the same event can be delivered twice
after a crash or a retry.

Retry and dead-letter policy lives in
`api/src/Shared/Wallow.Shared.Infrastructure.Core/Messaging/WolverineErrorHandlingExtensions.cs`, applied
via `opts.ConfigureStandardErrorHandling()`:

| Exception | Policy |
|-----------|--------|
| `TimeoutException` | Retry with cooldowns of 50 ms, 100 ms, 250 ms, then move to the error queue |
| `InvalidOperationException` | Retry 2 times, then move to the error queue |
| Any other exception | Retry once, then move to the error queue |

`ConfigureMessageLogging()` sets message-start logging to `Debug`.

## Message Middleware

Policies registered in Program.cs wrap every handler:

| Middleware | Location | Purpose |
|------------|----------|---------|
| `WolverineModuleTaggingMiddleware` | `api/src/Shared/Wallow.Shared.Infrastructure.Core/Middleware/WolverineModuleTaggingMiddleware.cs` | Tags the current activity with `wallow.module` / `wallow.tenant_id` and records messaging metrics |
| `TenantStampingMiddleware` | `api/src/Shared/Wallow.Shared.Infrastructure.Core/Middleware/TenantStampingMiddleware.cs` | Stamps the outgoing envelope with an `X-Tenant-Id` header from the tenant context |
| `TenantRestoringMiddleware` | `api/src/Shared/Wallow.Shared.Infrastructure.Core/Middleware/TenantRestoringMiddleware.cs` | Restores tenant context on the handling side from that header |
| `WolverineAuthorizationMiddleware` | `api/src/Wallow.Api/Middleware/WolverineAuthorizationMiddleware.cs` | Validates tenant context on external messages |
| FluentValidation | `opts.UseFluentValidation()` | Runs command validators before the handler |

Tenant stamping and restoring are why a handler can rely on the correct tenant being ambient even though
it runs on a background thread, detached from the originating HTTP request.

## Observability

The tagging middleware emits three metrics from the `Wallow.Messaging` meter
(`api/src/Shared/Wallow.Shared.Kernel/Diagnostics.cs`):

| Metric | Type | Tags |
|--------|------|------|
| `wallow.messaging.messages_total` | Counter | `message_type`, `module`, `status` |
| `wallow.messaging.message_duration` | Histogram (ms) | `message_type`, `module`, `status` |
| `wallow.messaging.domain_events_published_total` | Counter | `event_type` |

The `wallow.` prefix follows the branding prefix passed to `Diagnostics.Initialize(...)`, and Prometheus
renders the dots as underscores. See the [Observability Guide](../operations/observability.md) for
dashboards and tracing.

## Event Catalog

The full inventory of events, their payload schemas, and which module consumes each one is generated
from the code itself rather than maintained by hand.
`api/src/Shared/Wallow.Shared.Infrastructure/AsyncApi/EventFlowDiscovery.cs` reflects over an
explicit assembly list at startup — the same `HandlerAssemblies` set Wolverine is given, with
`Wallow.Shared.Contracts` appended because that assembly hosts the event types themselves but no
handlers. It finds every `IIntegrationEvent` in a `Wallow.Shared.Contracts.*` namespace and every
Wolverine handler that accepts one, and renders an AsyncAPI 3.0 document plus a Mermaid flow diagram
at dev-only endpoints. Sharing one list is deliberate: the generator used to run its own
`AppDomain.CurrentDomain.GetAssemblies()` scan, which saw a *different* set from Wolverine's
depending on what had been loaded by the time each ran.

Adding an event to `Wallow.Shared.Contracts` and a handler for it is all that is required — the catalog
picks both up on the next restart. See the [AsyncAPI Event Catalog](../integrations/asyncapi.md) for the
endpoints and the generator internals.

## Adding a New Integration Event

1. Add a `sealed record` inheriting `IntegrationEvent` under
   `api/src/Shared/Wallow.Shared.Contracts/{Module}/Events/`, past-tense name, primitives only.
2. Publish it with `IMessageBus.PublishAsync(...)` from the owning module, including `TenantId`.
3. Add a handler class in the consuming module's Application layer with a `Handle`/`HandleAsync` method
   taking the event first and its dependencies after. No registration needed.
4. Make the handler idempotent — delivery is at-least-once.
5. Restart the API and confirm the event and its consumer appear in the AsyncAPI catalog.

## Related Documentation

- [Module Creation](module-creation.md) — adding a module and its contracts
- [Background Jobs](background-jobs.md) — Hangfire for time-based work, Wolverine for event-driven work
- [AsyncAPI Event Catalog](../integrations/asyncapi.md) — generated event inventory and flow diagrams
- [Audit Events](../operations/audit-events.md) — writing audit records from event handlers
- [Observability](../operations/observability.md) — messaging metrics and traces
