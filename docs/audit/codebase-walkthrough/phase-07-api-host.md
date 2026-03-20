# Phase 7: API Host

**Scope:** `src/Wallow.Api/` (composition root / API host) and `tests/Wallow.Api.Tests/` (API-level tests)
**Status:** Not Started
**Files:** 24 source files, 19 test files

## How to Use This Document
- Work through files top-to-bottom within each section
- Check boxes as each file is reviewed and confirmed
- Add notes in the "Your Notes" column during review
- Mark status as: In Progress / Completed when done

## Source Files

### Entry Point & Module Registration

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 1 | [ ] | `src/Wallow.Api/Program.cs` | Application entry point and composition root. Configures the full ASP.NET middleware pipeline, Wolverine message bus, SignalR, Hangfire, health checks, and OpenTelemetry. | Middleware ordering (ExceptionHandler -> Serilog -> CORS -> Auth -> Tenant -> Authorization), Wolverine PostgreSQL durable outbox + RabbitMQ transport setup, Redis-backed SignalR | All module `.Api` projects, Shared.Kernel, Shared.Contracts, Serilog, Wolverine, Hangfire, StackExchange.Redis |
| 2 | [ ] | `src/Wallow.Api/WallowModules.cs` | Central registry that calls each module's `AddXxxModule()` and `InitializeXxxModuleAsync()` extension methods. Supports toggling modules via `Wallow:Modules` configuration. | `AddWallowModules` (DI registration), `InitializeWallowModulesAsync` (DB migrations), plugin system loading | All module Infrastructure.Extensions, Shared.Infrastructure.Plugins |
| 3 | [ ] | `src/Wallow.Api/Properties/AssemblyInfo.cs` | Exposes internals to the test project via `InternalsVisibleTo`. | `[assembly: InternalsVisibleTo("Wallow.Api.Tests")]` | None |

### Extensions

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 4 | [ ] | `src/Wallow.Api/Extensions/ServiceCollectionExtensions.cs` | Registers cross-cutting API services: ProblemDetails, OpenAPI/Scalar docs, CORS, health checks (PostgreSQL, Hangfire, Redis, Keycloak, RabbitMQ, S3), rate limiting, and OpenTelemetry. | `AddApiServices` (health checks + CORS), `AddWallowRateLimiting` (auth/upload/SCIM/global policies), `AddObservability` (OTLP tracing + metrics) | HealthChecks, RateLimitDefaults, OpenTelemetry, Scalar |
| 5 | [ ] | `src/Wallow.Api/Extensions/AsyncApiEndpointExtensions.cs` | Maps development-only AsyncAPI documentation endpoints that auto-discover event flows from Wallow assemblies. | `MapAsyncApiEndpoints` discovers event flows, serves JSON spec at `/asyncapi/v1.json`, Mermaid diagrams at `/asyncapi/v1/flows`, and an HTML viewer at `/asyncapi` | Shared.Infrastructure.Workflows.AsyncApi |
| 6 | [ ] | `src/Wallow.Api/Extensions/HangfireExtensions.cs` | Configures Hangfire with PostgreSQL storage and mounts the dashboard. | `AddHangfireServices` (PostgreSQL storage in `hangfire` schema), `UseHangfireDashboard` (at `/hangfire` with auth filter) | Hangfire, Hangfire.PostgreSql, HangfireDashboardAuthFilter |
| 7 | [ ] | `src/Wallow.Api/Extensions/RateLimitDefaults.cs` | Defines rate limit constants for auth (3/10min), upload (10/hr), SCIM (30/min), and global (1000/hr) policies. | Static constants only | None |

### Health Checks

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 8 | [ ] | `src/Wallow.Api/HealthChecks/HealthCheckMetricsPublisher.cs` | Publishes health check results as OpenTelemetry gauge metrics (`wallow.healthcheck.status`). | `PublishAsync` records 0/1 per check name using `System.Diagnostics.Metrics.Gauge` | Shared.Kernel.Diagnostics |
| 9 | [ ] | `src/Wallow.Api/HealthChecks/KeycloakHealthCheck.cs` | Verifies Keycloak reachability by hitting the realm's OpenID Connect discovery endpoint. | `CheckHealthAsync` calls `GET /realms/{realm}/.well-known/openid-configuration` | IHttpClientFactory, IConfiguration |
| 10 | [ ] | `src/Wallow.Api/HealthChecks/S3HealthCheck.cs` | Verifies S3-compatible storage reachability by listing buckets. Only registered when `StorageProvider` is S3. | `CheckHealthAsync` calls `s3Client.ListBucketsAsync()` | Amazon.S3 (IAmazonS3) |

### Hubs & Services (Real-time)

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 11 | [ ] | `src/Wallow.Api/Hubs/RealtimeHub.cs` | SignalR hub for real-time communication. Handles connection lifecycle, tenant-scoped group management, presence tracking, and page context updates. | `OnConnectedAsync`/`OnDisconnectedAsync` (presence), `JoinGroup`/`LeaveGroup` (tenant validation), `UpdatePageContext` (page-level presence), cross-tenant join rejection | IPresenceService, IRealtimeDispatcher, ITenantContext |
| 12 | [ ] | `src/Wallow.Api/Services/RedisPresenceService.cs` | Redis-backed implementation of `IPresenceService`. Tracks user connections, page contexts, and online status using tenant-scoped Redis keys with TTL. | `TrackConnectionAsync`/`RemoveConnectionAsync` (batched Redis ops), `GetOnlineUsersAsync`, `GetUsersOnPageAsync`, `IsUserOnlineAsync` | StackExchange.Redis (IConnectionMultiplexer) |
| 13 | [ ] | `src/Wallow.Api/Services/SignalRRealtimeDispatcher.cs` | Implements `IRealtimeDispatcher` by dispatching `RealtimeEnvelope` messages to users, groups, or tenants via SignalR. Sanitizes HTML in payloads before sending. | `SendToUserAsync`/`SendToGroupAsync`/`SendToTenantAsync`, recursive JSON node HTML sanitization via `IHtmlSanitizationService` | IHubContext<RealtimeHub>, IHtmlSanitizationService |

### Jobs

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 14 | [ ] | `src/Wallow.Api/Jobs/SystemHeartbeatJob.cs` | Simple recurring Hangfire job that logs a heartbeat message to confirm the system is alive. | `ExecuteAsync` logs timestamp via source-generated `LoggerMessage` | ILogger |

### Logging

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 15 | [ ] | `src/Wallow.Api/Logging/ModuleEnricher.cs` | Serilog enricher that extracts the module name from `SourceContext` namespace (e.g., `Wallow.Billing.* -> Module=Billing`). | `Enrich` parses `SourceContext` property, splits on `.`, takes second segment | Serilog.Core |
| 16 | [ ] | `src/Wallow.Api/Logging/PiiDestructuringPolicy.cs` | Serilog destructuring policy that redacts sensitive properties (Email, Password, Token, CreditCard, etc.) from structured log objects. | `TryDestructure` checks property names against a hashset of sensitive names, replaces with `[REDACTED]` | Serilog.Core |

### Middleware

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 17 | [ ] | `src/Wallow.Api/Middleware/ApiVersionRewriteMiddleware.cs` | Rewrites unversioned `/api/{path}` requests to `/api/v1/{path}` for backward compatibility with clients that don't specify an API version. | `InvokeAsync` checks for `/api/v{digit}` pattern, rewrites path if absent | None |
| 18 | [ ] | `src/Wallow.Api/Middleware/CorrelationIdMiddleware.cs` | Ensures every request has a correlation ID. Reads from `X-Correlation-Id` header or generates a new GUID. Pushes to Serilog LogContext and OpenTelemetry Activity tags. | `InvokeAsync` with `LogContext.PushProperty` and `Activity.SetTag` | Serilog.Context, System.Diagnostics |
| 19 | [ ] | `src/Wallow.Api/Middleware/GlobalExceptionHandler.cs` | Converts unhandled exceptions to RFC 7807 Problem Details responses. Maps domain exceptions to appropriate HTTP status codes (404, 422, 400, 401, 403). | `TryHandleAsync` with pattern matching: `EntityNotFoundException->404`, `BusinessRuleException->422`, `ValidationException->400`, `OperationCanceledException->499` | FluentValidation, Shared.Kernel.Domain |
| 20 | [ ] | `src/Wallow.Api/Middleware/HangfireDashboardAuthFilter.cs` | Authorization filter for the Hangfire dashboard. Allows all access in Development; requires authenticated admin role in other environments. | `Authorize` checks `IsDevelopment()` or `ClaimTypes.Role == "admin"` | Hangfire.Dashboard |
| 21 | [ ] | `src/Wallow.Api/Middleware/ModuleTaggingMiddleware.cs` | Tags OpenTelemetry spans with `wallow.module` by extracting the module name from the controller's namespace using a source-generated regex. | `InvokeAsync` reads `ControllerActionDescriptor`, matches `^Wallow\.(\w+)\.Api\b` | System.Diagnostics, GeneratedRegex |
| 22 | [ ] | `src/Wallow.Api/Middleware/SecurityHeadersMiddleware.cs` | Adds security headers (X-Content-Type-Options, X-Frame-Options, CSP, HSTS, Referrer-Policy, Permissions-Policy) to all responses. CSP varies by path (Hangfire, Scalar, SignalR). | `InvokeAsync` with `OnStarting` callback, path-based CSP selection | None |
| 23 | [ ] | `src/Wallow.Api/Middleware/TenantBaggageMiddleware.cs` | Propagates the resolved tenant ID to OpenTelemetry baggage and activity tags for distributed tracing. | `InvokeAsync` sets `Baggage.SetBaggage("wallow.tenant_id")` and `Activity.SetTag` | ITenantContext, OpenTelemetry |
| 24 | [ ] | `src/Wallow.Api/Middleware/WolverineAuthorizationMiddleware.cs` | Wolverine middleware that enforces tenant context on external (RabbitMQ) messages. Skips validation for in-process local messages. | `Before(Envelope)` checks `X-Tenant-Id` header on non-local messages | Wolverine |

## Test Files

### Extensions Tests

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 1 | [ ] | `tests/Wallow.Api.Tests/GlobalUsings.cs` | Global using declarations for FluentAssertions and NSubstitute | N/A - infrastructure |
| 2 | [ ] | `tests/Wallow.Api.Tests/Extensions/AsyncApiEndpointExtensionsTests.cs` | Tests for AsyncAPI endpoint registration | `AsyncApiEndpointExtensions.MapAsyncApiEndpoints` |
| 3 | [ ] | `tests/Wallow.Api.Tests/Extensions/HangfireExtensionsTests.cs` | Tests for Hangfire service registration | `HangfireExtensions.AddHangfireServices` |
| 4 | [ ] | `tests/Wallow.Api.Tests/Extensions/ResultExtensionsTests.cs` | Tests for result mapping extension methods | Result-to-HTTP response mapping |
| 5 | [ ] | `tests/Wallow.Api.Tests/Extensions/ServiceCollectionExtensionsTests.cs` | Tests for API service registration (CORS, health checks, OpenAPI) | `ServiceCollectionExtensions.AddApiServices` |

### Hub Tests

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 6 | [ ] | `tests/Wallow.Api.Tests/Hubs/RealtimeHubTests.cs` | Unit tests for SignalR hub logic | `RealtimeHub` connection lifecycle, group management, tenant validation |

### Integration Tests

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 7 | [ ] | `tests/Wallow.Api.Tests/Integration/ApiIntegrationTestCollection.cs` | xUnit collection definition for sharing `WallowApiFactory` across API integration tests | N/A - test infrastructure |
| 8 | [ ] | `tests/Wallow.Api.Tests/Integration/HealthCheckTests.cs` | Integration tests for health check endpoints | `/health`, `/health/ready`, `/health/live` endpoints |
| 9 | [ ] | `tests/Wallow.Api.Tests/Integration/RealtimeHubIntegrationTests.cs` | Integration tests for SignalR hub with real HTTP pipeline | End-to-end `RealtimeHub` via WebSocket |

### Job Tests

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 10 | [ ] | `tests/Wallow.Api.Tests/Jobs/SystemHeartbeatJobTests.cs` | Tests for the heartbeat job execution | `SystemHeartbeatJob.ExecuteAsync` |

### Logging Tests

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 11 | [ ] | `tests/Wallow.Api.Tests/Logging/ModuleEnricherTests.cs` | Tests for Serilog module enricher logic | `ModuleEnricher.Enrich` namespace-to-module extraction |

### Middleware Tests

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 12 | [ ] | `tests/Wallow.Api.Tests/Middleware/ApiVersionRewriteMiddlewareTests.cs` | Tests for API version URL rewriting | `ApiVersionRewriteMiddleware` path rewrite logic |
| 13 | [ ] | `tests/Wallow.Api.Tests/Middleware/GlobalExceptionHandlerTests.cs` | Tests for exception-to-ProblemDetails mapping | `GlobalExceptionHandler` with various exception types |
| 14 | [ ] | `tests/Wallow.Api.Tests/Middleware/HangfireDashboardAuthFilterTests.cs` | Tests for Hangfire dashboard authorization | `HangfireDashboardAuthFilter.Authorize` in dev vs prod |
| 15 | [ ] | `tests/Wallow.Api.Tests/Middleware/ModuleTaggingMiddlewareTests.cs` | Tests for OpenTelemetry module tagging | `ModuleTaggingMiddleware` namespace regex extraction |
| 16 | [ ] | `tests/Wallow.Api.Tests/Middleware/RateLimitDefaultsTests.cs` | Tests for rate limit constant values | `RateLimitDefaults` constants |
| 17 | [ ] | `tests/Wallow.Api.Tests/Middleware/SecurityHeadersMiddlewareTests.cs` | Tests for security header injection | `SecurityHeadersMiddleware` header values and path-based CSP |

### Service Tests

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 18 | [ ] | `tests/Wallow.Api.Tests/Services/RedisPresenceServiceTests.cs` | Tests for Redis-backed presence tracking | `RedisPresenceService` connection tracking, page context, online status |
| 19 | [ ] | `tests/Wallow.Api.Tests/Services/SignalRRealtimeDispatcherTests.cs` | Tests for SignalR message dispatching and HTML sanitization | `SignalRRealtimeDispatcher` send methods and payload sanitization |
