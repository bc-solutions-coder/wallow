# Wallow.Api

## Module Responsibility

The API host project. Owns the application entry point (`Program.cs`), the ASP.NET middleware pipeline, DI composition root, Wolverine message bus configuration, SignalR real-time hub, Hangfire background jobs, health checks, and OpenAPI/Scalar documentation. This project wires all modules together but contains no business logic of its own.

## Layer Rules

This is not a Clean Architecture layer -- it is the **composition root**.

- **May** reference every module's `.Api` project (and transitively their Application/Infrastructure/Domain layers).
- **May** reference `Wallow.Shared.Kernel` and `Wallow.Shared.Contracts`.
- **Must not** contain domain entities, CQRS handlers, or repository implementations. Those belong in modules.
- **Must not** contain business logic. Controller-level code should delegate to Wolverine (`IMessageBus`) immediately.
- Extension methods in `Extensions/` configure DI registrations (CORS, OpenAPI, health checks, OpenTelemetry, Hangfire). Keep them focused on infrastructure wiring.

## Key Patterns

- **Middleware pipeline order** (in `Program.cs`): ExceptionHandler -> Serilog request logging -> OpenAPI (dev only) -> CORS -> Health checks -> Authentication (Keycloak JWT) -> TenantResolutionMiddleware -> PermissionExpansionMiddleware -> Authorization -> Hangfire dashboard -> Controllers + SignalR hub. Order matters -- do not rearrange.
- **Wolverine setup**: Configures PostgreSQL durable outbox, EF Core transaction integration, FluentValidation middleware, and handler assembly discovery for each module. Defaults to in-memory transport; set `ModuleMessaging:Transport` to `RabbitMq` to enable RabbitMQ transport.
- **SignalR**: Single `RealtimeHub` at `/hubs/realtime` with Redis backplane. Authenticated via JWT query string parameter for WebSocket upgrade. Presence tracking via `RedisPresenceService`.
- **Hangfire**: PostgreSQL storage, dashboard at `/hangfire` (auth-filtered). Modules register recurring jobs via `IRecurringJobRegistration`. API registers `SystemHeartbeatJob` directly.
- **Health checks**: `/health` (all), `/health/ready` (PostgreSQL, Hangfire, Redis, Keycloak; RabbitMQ only when enabled), `/health/live` (liveness only).
- **Error handling**: `GlobalExceptionHandler` maps domain exceptions to RFC 7807 Problem Details. `EntityNotFoundException` -> 404, `BusinessRuleException` -> 422, etc.

## Dependencies

- **Depends on**: All module `.Api` projects (Identity, Notifications, Billing; Email commented out), `Wallow.Shared.Kernel`, `Wallow.Shared.Contracts`.
- **Depended on by**: Test projects (`Wallow.Api.Tests`, `Wallow.Tests.Common`) reference this via `WebApplicationFactory<Program>`.

## Constraints

- Do not add business logic here. If you need a new feature, add it to the appropriate module.
- Do not change middleware ordering without understanding the full pipeline. Authentication must precede tenant resolution, which must precede permission expansion, which must precede authorization.
- Do not add direct module-to-module references. Cross-module communication goes through Wolverine integration events defined in `Shared.Contracts`.
- The `Program` class is `partial` to support `WebApplicationFactory<Program>` in integration tests -- do not remove this.
- When RabbitMQ transport is enabled, exchange/queue bindings are declared in `Program.cs`. When adding a new integration event type, add its `PublishMessage` routing there.
