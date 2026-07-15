# Wallow.Api

## Overview

Wallow.Api is the main entry point and orchestration host for the Wallow modular monolith. It wires together autonomous modules, coordinates inter-module messaging via Wolverine, manages cross-cutting concerns (authentication, multi-tenancy, observability), and provides the unified API surface.

## Key Concepts

### Module Wiring

Modules are registered through `WallowModules.cs`, which calls each module's extension method (e.g., `AddIdentityModule()`, `AddBillingModule()`). These methods encapsulate module-specific services, infrastructure, and event consumers.

### CQRS via Wolverine

Wolverine provides the unified mediator pattern for commands and queries across all modules. Commands are validated via FluentValidation before handlers execute. The durable outbox pattern (PostgreSQL) guarantees at-least-once delivery.

### Multi-tenancy

`TenantResolutionMiddleware` resolves the tenant from the JWT `org_id` claim. The resolved tenant is stored in `ITenantContext` and injected into services that need to filter data per organization.

### Real-time Communication

- **SignalR**: `RealtimeHub` at `/hubs/realtime` broadcasts events to connected clients. Redis (Valkey) acts as the backplane for multi-instance deployments.
- **SSE**: Server-Sent Events endpoint at `/events` for lightweight real-time streaming.

### Background Jobs

Hangfire handles scheduled and recurring background jobs. The API provides a dashboard at `/hangfire` for monitoring. Recurring jobs include system heartbeat, failed email retry, OpenIddict token pruning, and expired invitation pruning.

### Workflow Engine

Elsa workflow engine integration for configurable business processes. Enabled by default, disabled via `Elsa:Enabled` config or in Testing environment.

## Middleware Pipeline

```
1. ExceptionHandler           Catches unhandled exceptions (RFC 7807 Problem Details)
2. SerilogRequestLogging      Structured logging of HTTP requests
3. CorrelationIdMiddleware    Read/generate X-Correlation-Id
4. SetupMiddleware            Redirect to setup wizard when admin bootstrap pending
5. SecurityHeadersMiddleware  CSP, X-Content-Type-Options, etc.
6. ApiVersionRewriteMiddleware  /api/foo -> /api/v1/foo backward compat
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
23. Elsa Workflows            (when enabled)
24. SignalR Hub               /hubs/realtime
25. SSE Endpoint              /events
```

## Key Types

- **`WallowModules`** - Central module registration and initialization
- **`GlobalExceptionHandler`** - RFC 7807 Problem Details error responses
- **`SignalRRealtimeDispatcher`** - `IRealtimeDispatcher` implementation broadcasting events to SignalR clients
- **`RedisPresenceService`** - User presence tracking via Redis
- **`RedisSseDispatcher`** - SSE dispatcher backed by Redis pub/sub
- **`SystemHeartbeatJob`** - Periodic health check job (every 5 minutes)
- **`ServiceCollectionExtensions`** - `AddApiServices()`, `AddObservability()`
- **`HangfireExtensions`** - `AddHangfireServices()` with PostgreSQL storage

## Getting Started

### Prerequisites
- .NET 10 SDK
- Docker and Docker Compose (for infrastructure)

### 1. Start Infrastructure

```bash
cd docker && docker compose up -d
```

This starts PostgreSQL, Valkey (Redis-compatible), GarageHQ (S3), Mailpit, and Grafana.

### 2. Run the API

```bash
dotnet run --project src/Wallow.Api
```

The API starts on **http://localhost:5000** with:
- **API Documentation**: http://localhost:5000/scalar (dev only)
- **Health Checks**: `/health`, `/health/ready`, `/health/live`, `/health/startup`
- **Background Jobs**: http://localhost:5000/hangfire
- **Real-time Hub**: ws://localhost:5000/hubs/realtime
- **SSE**: http://localhost:5000/events

### 3. Run Tests

```bash
./scripts/run-tests.sh
./scripts/run-tests.sh api
```
