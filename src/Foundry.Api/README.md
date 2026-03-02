# Foundry.Api

## Overview

Foundry.Api is the main entry point and orchestration host for the Foundry modular monolith. It wires together autonomous modules, coordinates inter-module messaging via RabbitMQ, manages cross-cutting concerns like authentication and multi-tenancy, and provides the unified API surface for the entire platform.

The API is built on ASP.NET Core with a sophisticated middleware pipeline that handles exception handling, structured logging, authentication, tenant resolution, authorization, and background job coordination.

## Key Concepts

### Module Wiring
Modules are registered into the DI container through extension methods (e.g., `AddIdentityModule()`, `AddBillingModule()`). These methods encapsulate module-specific services, infrastructure, and event consumers. This design keeps modules loosely coupled and independently deployable.

### CQRS via Wolverine
Wolverine provides a unified mediator pattern for commands and queries across all modules. Commands are validated via FluentValidation before handlers execute, ensuring consistent error handling and validation rules. The durable outbox pattern (PostgreSQL) guarantees messages reach RabbitMQ even if delivery fails.

### Multi-tenancy
A custom middleware pipeline resolves the tenant from JWT claims (the `org` claim). The resolved tenant is stored in `ITenantContext` and injected into services that need to filter data per organization.

### Real-time Communication
SignalR hub (`RealtimeHub`) at `/hubs/realtime` broadcasts events to connected clients. Redis acts as the backplane for multi-instance deployments, ensuring all server instances can publish and receive messages across WebSocket connections.

### Background Jobs
Hangfire handles scheduled and recurring background jobs. Modules register their own recurring jobs via `RegisterRecurringJobs()`. The API provides a dashboard at `/hangfire` for monitoring job execution.

## Architecture

The middleware pipeline is carefully ordered to ensure proper request flow:

```
1. ExceptionHandler         → GlobalExceptionHandler catches unhandled exceptions
2. SerilogRequestLogging    → Structured logging of HTTP requests
3. OpenAPI/Scalar (dev)     → API documentation UI at /scalar
4. CORS                     → Allow cross-origin requests per environment
5. HealthChecks             → /health, /health/ready, /health/live endpoints
6. Authentication           → Keycloak OIDC JWT validation
7. TenantResolution         → Extract org claim → populate ITenantContext
8. PermissionExpansion      → Expand Keycloak roles to PermissionType claims
9. Authorization            → Enforce [HasPermission] attributes
10. HangfireDashboard       → Admin UI at /hangfire
11. Controllers             → Route HTTP requests to action methods
12. SignalR Hub             → WebSocket connection handling at /hubs/realtime
```

### Entry Point: Program.cs
Configures all services and middleware. Key responsibilities:
- Bootstrap Serilog for structured logging
- Wire up Wolverine with PostgreSQL persistence and RabbitMQ transport
- Register module assemblies for handler discovery (critical for modular monoliths)
- Configure SignalR with Redis backplane
- Register all modules via extension methods
- Initialize modules and set up the middleware pipeline

## Key Types

### Program.cs
- **Main entry point** with service registration and middleware configuration
- **Wolverine CQRS setup** with PostgreSQL durable outbox and RabbitMQ transport
- **Module registration** through extension methods (e.g., `AddIdentityModule`, `AddBillingModule`)
- **Handler discovery** from module Application assemblies (essential for handler visibility)
- **SignalR and Redis** configuration for real-time features
- **Middleware pipeline** ordering for authentication, authorization, and tenant resolution

### GlobalExceptionHandler
- **RFC 7807 Problem Details** format for consistent error responses
- Catches all unhandled exceptions from the middleware pipeline
- Returns structured error responses with status codes, type URIs, and detailed messages

### SignalRRealtimeDispatcher
- Implementation of `IRealtimeDispatcher` contract
- Broadcasts real-time events (e.g., order updates, notifications) to connected SignalR clients
- Integrates with the event sourcing systems across modules

### RedisPresenceService
- Tracks user presence (online/offline) using Redis
- Supports real-time presence updates across multiple server instances
- Allows clients to know which users are currently online

### Extensions/ServiceCollectionExtensions.cs
- `AddApiServices()` → Configures health checks, controllers, CORS, exception handling
- `AddObservability()` → Sets up OpenTelemetry tracing, metrics, and Serilog
- `AddHangfireServices()` → Configures Hangfire with PostgreSQL storage

### Extensions/HangfireExtensions.cs
- `RegisterRecurringJobs()` → Discovers and registers recurring jobs from all modules
- `SystemHeartbeatJob` → Periodic health check job (every 5 minutes)

## Libraries & Dependencies

### Core Framework
| Library | Version | Purpose |
|---------|---------|---------|
| ASP.NET Core | 10.0 | Web framework |
| Microsoft.Extensions.* | 10.0.2 | DI, Configuration, Hosting |

### CQRS & Messaging
| Library | Version | Purpose |
|---------|---------|---------|
| WolverineFx | 5.11.0 | Mediator + message bus |
| WolverineFx.RabbitMQ | 5.11.0 | RabbitMQ transport for async messaging |
| WolverineFx.EntityFrameworkCore | 5.11.0 | EF Core integration for transaction support |
| WolverineFx.Postgresql | 5.11.0 | Durable outbox/inbox on PostgreSQL |
| WolverineFx.FluentValidation | 5.11.0 | Automatic command validation middleware |

### Authentication & Authorization
| Library | Version | Purpose |
|---------|---------|---------|
| Keycloak.AuthServices.Authentication | 2.5.3 | OIDC JWT validation |
| Microsoft.AspNetCore.Authentication.JwtBearer | 10.0.2 | JWT Bearer middleware |

### Real-time
| Library | Version | Purpose |
|---------|---------|---------|
| SignalR | (built-in) | WebSocket hub for real-time updates |
| Microsoft.AspNetCore.SignalR.StackExchangeRedis | 10.0.2 | Redis backplane for multi-instance deployments |
| StackExchange.Redis | 2.8.41 | Redis client for presence and backplane |

### Background Jobs
| Library | Version | Purpose |
|---------|---------|---------|
| Hangfire.AspNetCore | 1.8.22 | Background job processing and scheduling |
| Hangfire.PostgreSql | 1.20.13 | PostgreSQL job storage and state management |

### Observability
| Library | Version | Purpose |
|---------|---------|---------|
| Serilog.AspNetCore | 10.0.0 | Structured logging with context enrichment |
| Serilog.Sinks.Console | 10.0.0 | Console sink with formatted output |
| Serilog.Sinks.OpenTelemetry | Latest | OpenTelemetry sink for centralized logging |
| OpenTelemetry.* | 1.12.0 | Distributed tracing and metrics |
| OpenTelemetry.Instrumentation.AspNetCore | 1.12.0 | ASP.NET Core request tracing |
| OpenTelemetry.Instrumentation.EntityFrameworkCore | 1.10.0-beta.1 | Database query tracing |
| OpenTelemetry.Instrumentation.Http | 1.12.0 | HTTP client tracing |
| OpenTelemetry.Instrumentation.Runtime | 1.12.0 | Runtime metrics (GC, threads, memory) |

### API Documentation
| Library | Version | Purpose |
|---------|---------|---------|
| Microsoft.AspNetCore.OpenApi | 10.0.2 | OpenAPI/Swagger schema generation |
| Scalar.AspNetCore | 2.12.32 | Interactive API documentation UI |

### Health Checks
| Library | Version | Purpose |
|---------|---------|---------|
| AspNetCore.HealthChecks.NpgSql | Latest | PostgreSQL database connectivity check |
| AspNetCore.HealthChecks.Rabbitmq | Latest | RabbitMQ broker connectivity check |
| AspNetCore.HealthChecks.Hangfire | Latest | Hangfire job processing health check |
| AspNetCore.HealthChecks.Redis | Latest | Redis connectivity check |

## Integration Points

### Wolverine Durable Outbox (PostgreSQL)
- Commands published via Wolverine are persisted to a `wolverine` schema table
- Guarantees at-least-once delivery even if the API crashes after a command is published
- PostgreSQL schema is auto-managed by Wolverine; migrations run on startup

### RabbitMQ Exchanges & Queues
Events published by each module flow through topic exchanges:
- `identity-events` → Published by Identity module (UserRegisteredEvent, etc.)
- `billing-events` → Published by Billing module (InvoiceCreatedEvent, PaymentReceivedEvent, etc.)
- `communications-events` → Published by Communications module (EmailSentEvent, NotificationCreatedEvent, etc.)

Consumer queues that the API listens to:
- `communications-inbox` → For Communications module consumers
- `billing-inbox` → For Billing module consumers
- `test-inbox` → For integration test event handlers (Testing environment only)

### Redis for Real-time
- **SignalR Backplane**: Coordinates WebSocket messages across multiple API instances
- **Presence Service**: Tracks online/offline user status
- **Channel Prefix**: All Redis channels use `Foundry` prefix to avoid collisions in shared Redis instances

### Module Event Consumers
Each module publishes and consumes domain events:
- **Explicit registration** in Program.cs ensures handlers are discovered even in modular architectures
- **Handler discovery** includes module Application assemblies (e.g., `Foundry.Billing.Application`)
- **Validation** via FluentValidation runs automatically on all commands before handlers
- **Transaction support** integrates Wolverine messages with EF Core transactions

### Hangfire Scheduler
- Modules register recurring jobs in their `RegisterRecurringJobs()` implementations
- API-level recurring jobs (e.g., SystemHeartbeatJob) run on a schedule
- Dashboard at `/hangfire` for admin monitoring and manual job triggering

## Getting Started

### Prerequisites
- .NET 10 SDK
- Docker & Docker Compose (for infrastructure)
- PostgreSQL 18, RabbitMQ, Redis, Keycloak (all managed by docker-compose)

### 1. Start Infrastructure
```bash
cd docker
docker compose up -d
```

This starts:
- **PostgreSQL** (port 5432) - Primary database
- **RabbitMQ** (port 5672) - Message broker; admin UI at http://localhost:15672
- **Redis/Valkey** (port 6379) - Caching and SignalR backplane
- **Keycloak** (port 8080) - Identity provider
- **Mailpit** (port 8025) - Email testing UI
- **Grafana** (port 3000) - Observability dashboard

### 2. Run the API
```bash
dotnet run --project src/Foundry.Api
```

The API starts on **http://localhost:5000** with the following endpoints:
- **Home**: http://localhost:5000/ (JSON info endpoint)
- **API Documentation**: http://localhost:5000/scalar (interactive Scalar UI)
- **OpenAPI Schema**: http://localhost:5000/openapi/v1.json
- **Health Checks**:
  - `/health` → Overall system health
  - `/health/ready` → Startup readiness (DB, RabbitMQ, Redis connected)
  - `/health/live` → Liveness probe (just responds 200)
- **Background Jobs**: http://localhost:5000/hangfire (Hangfire dashboard)
- **Real-time Hub**: ws://localhost:5000/hubs/realtime (WebSocket endpoint)

### 3. Authenticate
Keycloak is available at http://localhost:8080. Default credentials:
- **Realm**: foundry
- **User**: admin@foundry.dev
- **Password**: Admin123!

Use the Keycloak admin console to create users, assign roles, and generate JWT tokens. Include the JWT in the `Authorization: Bearer <token>` header when calling protected endpoints.

### 4. Monitor & Debug
- **API Logs**: Console output shows structured logs with request/response details
- **RabbitMQ Admin**: http://localhost:15672 (see `docker/.env`) - Monitor exchanges, queues, messages
- **Hangfire Dashboard**: http://localhost:5000/hangfire - View scheduled and completed jobs
- **Mailpit**: http://localhost:8025 - Inspect sent emails
- **Grafana**: http://localhost:3000 (admin / admin) - View traces, metrics, and logs

### 5. Run Tests
```bash
# Run all tests
dotnet test

# Run API-level integration tests
dotnet test tests/Foundry.Api.Tests

# Run specific module tests
dotnet test tests/Modules/Billing/Foundry.Billing.Tests
```

## Architecture Decision Records

For detailed design rationale, see:
- `docs/plans/2026-02-04-foundry-pivot-design.md` - Overall platform architecture
- `docs/DEVELOPER_GUIDE.md` - How to work with the codebase and add modules
