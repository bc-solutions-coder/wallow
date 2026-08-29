# Wallow backend

The .NET 10 solution (`Wallow.slnx`): a modular monolith where each module is an autonomous
bounded context following Clean Architecture. Modules communicate through Wolverine in-memory
events via `Shared.Contracts`, never direct references. Each module owns its own PostgreSQL
schema. Tenant isolation is row-level: every tenant-scoped entity carries a `TenantId` enforced
by EF global query filters, with the tenant resolved from JWT claims per request.

## Layout

```
src/
├── Wallow.Api/                  # REST API host: modules, Wolverine, OpenIddict resource server
├── Wallow.AppHost/              # .NET Aspire host orchestrating the API, React apps, and infra
├── Wallow.MigrationService/     # Applies EF migrations for all module DbContexts
├── Wallow.SeederService/        # Seeds roles, scopes, admin, and OIDC clients from seed.json
├── Wallow.ServiceDefaults/      # Aspire defaults: telemetry, health checks, resilience
├── Modules/
│   ├── Identity/                # Auth, users, organizations, RBAC
│   ├── Storage/                 # File storage (S3-compatible)
│   ├── Notifications/           # In-app, email, SMS, and push notifications
│   ├── Announcements/           # Tenant-scoped announcements and the global changelog
│   ├── Inquiries/               # Contact-form inquiry submission
│   ├── ApiKeys/                 # API keys for service accounts
│   └── Branding/                # Per-client login branding
└── Shared/
    ├── Wallow.Shared.Contracts/                 # Cross-module integration events
    ├── Wallow.Shared.Kernel/                    # DDD building blocks, multi-tenancy, JWT claim helpers
    ├── Wallow.Shared.Api/                       # Shared API utilities (Result → IActionResult, health check)
    ├── Wallow.Shared.Infrastructure/            # Settings framework and module coordination
    ├── Wallow.Shared.Infrastructure.Core/       # Persistence, caching, and messaging
    ├── Wallow.Shared.Infrastructure.BackgroundJobs/  # Hangfire-backed IJobScheduler
    └── Wallow.Shared.Infrastructure.Plugins/    # Plugin loading and extension points
```

Each module has four layers. Domain depends on nothing; Application depends on Domain;
Infrastructure and Api sit on top. The API is headless: the React apps in `../apps/` are the
only UIs.

Deep dives: [Architecture assessment](../docs/architecture/assessment.md) ·
[Module creation](../docs/architecture/module-creation.md)

## What's inside

| Area | How it works |
|------|--------------|
| Clean Architecture | Strict per-module dependency rules, verified by architecture tests |
| Domain-Driven Design | Entities, value objects, domain events, bounded contexts |
| CQRS | Command/query separation with Wolverine as the mediator |
| Multi-tenancy | Row-level isolation via `TenantId` global query filters; tenant resolved from JWT claims, admin override via `X-Tenant-Id` |
| Events | Wolverine in-memory integration events between modules |
| Identity & RBAC | OpenIddict + ASP.NET Core Identity; roles granted per organization |
| Real-time | SignalR push |
| Observability | Serilog structured logging, OpenTelemetry tracing, [Grafana dashboards](../docs/operations/observability.md) |
| Audit trail | Entity change auditing via Audit.NET |
| Background jobs | `IJobScheduler` backed by Hangfire |

## Tech stack

| Purpose | Technology |
|---------|------------|
| Framework | .NET 10 |
| Database | PostgreSQL 18 |
| ORM | EF Core (writes tracked, reads `NoTracking` via `IReadDbContext<T>`) |
| CQRS & messaging | Wolverine (in-memory) |
| Caching | Valkey (Redis-compatible) |
| Identity | OpenIddict + ASP.NET Core Identity |
| Real-time | SignalR |
| Validation | FluentValidation |
| Logging & tracing | Serilog, OpenTelemetry |
| Testing | xUnit, Testcontainers, AwesomeAssertions |

## Running

From the repo root:

```bash
pnpm backend                              # Aspire AppHost: API + both React apps + migration + seeder
pnpm backend:infra                        # infra only (Postgres, Valkey, GarageHQ, Mailpit, Grafana)
dotnet run --project api/src/Wallow.Api   # the API alone → http://localhost:5001
```

Day-to-day backend commands (seed, format, migrations) live in [`CLAUDE.md`](CLAUDE.md).

## Testing

There are 15 xUnit test assemblies under `tests/` covering unit, integration, and architecture
tiers, plus the shared `Wallow.Tests.Common` helpers and a BenchmarkDotNet project.

From the repo root:

```bash
./scripts/run-tests.sh                    # fast suites, coverage runsettings applied
./scripts/run-tests.sh all                # the same plus every Category=Integration test (needs Docker)
./scripts/run-tests.sh identity           # one module
```

A bare run filters out `Category=Integration` and says so in its output. Only `all` (or
`integration`) exercises the Wolverine handler-codegen guards and the Testcontainers suites.
Always use the script rather than bare `dotnet test`; it applies
`api/tests/coverage.runsettings`, which excludes generated code from coverage.

Details: [Testing guide](../docs/development/testing.md)

## Configuration

Backend configuration flows through standard .NET mechanisms, loaded in order:
`appsettings.json` → `appsettings.{Environment}.json` → environment variables → user secrets
(dev only). Any setting can be overridden with `Section__Key` environment variables.

| Area | What it controls |
|------|------------------|
| Database | PostgreSQL and Valkey connection strings |
| Email | SMTP host, port, TLS, sender defaults |
| Storage | S3 endpoint, bucket, ClamAV virus scanning |
| Observability | OpenTelemetry OTLP endpoints, service name |
| CORS | Allowed origins for API requests |

Full reference with Docker and Kubernetes examples:
[Configuration guide](../docs/getting-started/configuration.md)
