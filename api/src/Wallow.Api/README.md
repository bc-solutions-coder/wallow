# Wallow.Api

## Overview

Wallow.Api is the main entry point and orchestration host for the Wallow modular monolith. It wires together autonomous modules, coordinates inter-module messaging via Wolverine, manages cross-cutting concerns (authentication, multi-tenancy, observability), and provides the unified API surface.

## Key Concepts

### Module Wiring

Modules are registered through `WallowModules.cs`, which calls each module's extension method (e.g., `AddIdentityModule()`, `AddNotificationsModule()`). These methods encapsulate module-specific services, infrastructure, and event consumers.

### CQRS via Wolverine

Wolverine provides the unified mediator pattern for commands and queries across all modules. Commands are validated via FluentValidation before handlers execute. The durable outbox pattern (PostgreSQL) guarantees at-least-once delivery.

### Multi-tenancy

`TenantResolutionMiddleware` resolves the tenant from the JWT `org_id` claim. The resolved tenant is stored in `ITenantContext` and injected into services that need to filter data per organization.

### Real-time Communication

- **SignalR**: `RealtimeHub` at `/hubs/realtime` broadcasts events to connected clients. Valkey (Redis-compatible) acts as the backplane for multi-instance deployments.
- **SSE**: Server-Sent Events endpoint at `/events` for lightweight real-time streaming.

### Background Jobs

Hangfire handles scheduled and recurring background jobs. The API provides a dashboard at `/hangfire` for monitoring. `Program.cs` registers up to five recurring jobs: system heartbeat, OpenIddict token pruning, expired invitation pruning, session pruning, and — only when the `Modules.Notifications` feature flag is enabled — failed email retry.

## Middleware Pipeline

```
1. ExceptionHandler           Catches unhandled exceptions (RFC 7807 Problem Details)
2. SerilogRequestLogging      Structured logging of HTTP requests
3. CorrelationIdMiddleware    Read/generate X-Correlation-Id
4. SetupMiddleware            Redirect to setup wizard when admin bootstrap pending
5. SecurityHeadersMiddleware  CSP, X-Content-Type-Options, etc.
6. ApiVersionRewriteMiddleware  Prepends /v1 to unversioned paths (see note below)
7. Routing
8. OpenAPI/Scalar (dev)       API documentation UI at /scalar
9. CORS
10. Health Checks             /health, /health/ready, /health/live, /health/startup
11. Rate Limiting             (non-dev/testing only)
12. ApiKeyAuthentication      X-Api-Key header check
13. Authentication            OpenIddict JWT validation
14. TenantResolution          org_id claim -> ITenantContext
15. TenantBaggage             Activity tag + W3C Baggage propagation
16. ScimAuthentication        Bearer token for /scim/v2/* endpoints
17. PermissionExpansion       Roles -> PermissionType claims
18. Authorization             [HasPermission] attributes
19. ModuleTagging             wallow.module observability tag
20. ServiceAccountTracking    Usage tracking for service accounts
21. HangfireDashboard
22. Controllers
23. SignalR Hub               /hubs/realtime
24. SSE Endpoint              /events
```

> [!NOTE]
> `ApiVersionRewriteMiddleware` rewrites the **post-PathBase** path and only ever prepends `/v1`;
> it never adds `/api`. At the default `PathBase=""` the routes are `/v1/...`, so a request to
> `/api/users` is rewritten to `/v1/api/users` and 404s. The `/api → /api/v1` shape only appears
> under the opt-in reverse-proxy PathBase, which is off by default.

## Key Types

- **`WallowModules`** - Central module registration and initialization
- **`GlobalExceptionHandler`** - RFC 7807 Problem Details error responses
- **`SignalRRealtimeDispatcher`** - `IRealtimeDispatcher` implementation broadcasting events to SignalR clients
- **`RedisPresenceService`** - User presence tracking via Valkey
- **`RedisSseDispatcher`** - SSE dispatcher backed by Valkey pub/sub
- **`SystemHeartbeatJob`** - Periodic health check job (every 5 minutes)
- **`ServiceCollectionExtensions`** - `AddApiServices()`, `AddObservability()`
- **`HangfireExtensions`** - `AddHangfireServices()` with PostgreSQL storage

## Getting Started

### Prerequisites
- .NET 10 SDK
- Node 24 + pnpm (the repo-root scripts below are pnpm scripts)
- Docker and Docker Compose (for infrastructure)

### 1. Run everything via the Aspire host

`Wallow.AppHost` (`api/src/Wallow.AppHost/Program.cs`) is the documented entrypoint — it
orchestrates the API, the frontends, the migration and seeder services, and the infrastructure
containers:

```bash
pnpm backend
```

### 2. Or: infrastructure and the API separately

```bash
pnpm backend:infra          # docker compose up -d; pnpm backend:infra:down to stop
dotnet run --project api/src/Wallow.Api
```

`docker/docker-compose.yml` defines eight services: `postgres`, `valkey` (Redis-compatible),
`garage` (S3), `mailpit`, `clamav`, `alloy`, `grafana-lgtm` (Grafana on host port 3001), and
`docs` (the DocFX site on host port 5004).

The API starts on **http://localhost:5001** with:
- **API Documentation**: http://localhost:5001/scalar (dev only)
- **Health Checks**: `/health`, `/health/ready`, `/health/live`, `/health/startup`
- **Background Jobs**: http://localhost:5001/hangfire
- **Real-time Hub**: ws://localhost:5001/hubs/realtime
- **SSE**: http://localhost:5001/events

### 3. Run Tests

```bash
./scripts/run-tests.sh
./scripts/run-tests.sh api
```
