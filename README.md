<div align="center">

<img src="assets/piggy-icon.svg" alt="Wallow" width="120" />

# Wallow

**A production-ready .NET modular monolith for building multi-tenant SaaS products.**

Fork it. Add your domain modules. Deploy.

<!-- Shields: replace with your own URLs or remove any that don't apply -->
[![CI](https://github.com/bc-solutions-coder/Wallow/actions/workflows/ci.yml/badge.svg)](https://github.com/bc-solutions-coder/Wallow/actions/workflows/ci.yml)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![PostgreSQL](https://img.shields.io/badge/PostgreSQL-18-4169E1?logo=postgresql&logoColor=white)](https://www.postgresql.org/)
[![License](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE)
[![Docker](https://img.shields.io/badge/Docker-ready-2496ED?logo=docker&logoColor=white)](docker/)


</div>

---

## Fork This

Wallow is a base platform — the intended workflow is to **fork it and build your product on top**.

1. Fork this repository on GitHub
2. Customize branding and config via `branding.json` and `appsettings.json`
3. Add your domain modules alongside the built-in ones
4. Pull upstream improvements from this repo as the platform evolves

See the [Fork Guide](docs/getting-started/fork-guide.md) for step-by-step instructions.

---

## What is Wallow?

Wallow provides the cross-cutting infrastructure every SaaS product needs out of the box: identity management, billing, notifications, messaging, file storage, and multi-tenant data isolation. You write the business logic.

New products are created by forking Wallow and adding domain-specific modules. The base platform stays generic -- improvements to shared infrastructure can be pulled from upstream into forks.

## Architecture

Wallow is a **modular monolith**. Each module is an autonomous bounded context that follows Clean Architecture internally.

```
src/
├── Wallow.Api/                  # Host, middleware, routing
├── Modules/
│   ├── Identity/                 # Auth, users, organizations, RBAC
│   ├── Billing/                  # Payments, invoices, subscriptions
│   ├── Storage/                  # File storage (S3, local)
│   ├── Notifications/            # In-app and push notifications
│   ├── Messaging/                # User-to-user conversations
│   ├── Announcements/            # System-wide announcements
│   └── Inquiries/                # Inquiry and question submission
└── Shared/
    ├── Contracts/                # Cross-module event definitions
    └── Kernel/                   # Base classes, shared abstractions
```

Each module is structured in four layers:

| Layer | Responsibility | Dependencies |
|-------|----------------|--------------|
| **Domain** | Entities, value objects, domain events | None |
| **Application** | Commands, queries, handlers, DTOs | Domain |
| **Infrastructure** | EF Core, Dapper, consumers, services | Application |
| **API** | Controllers, request/response contracts | Application |

Modules communicate through events over RabbitMQ via `Shared.Contracts` -- never direct references. Each module owns its own database schema.

## Modules

| Module | Responsibility |
|--------|----------------|
| **Identity** | Authentication, users, organizations, roles, RBAC via OpenIddict + ASP.NET Core Identity |
| **Billing** | Payments, invoices, subscription lifecycle |
| **Storage** | File storage abstraction (S3-compatible, local filesystem) |
| **Notifications** | In-app and push notifications, delivery preferences |
| **Messaging** | User-to-user conversations and threads |
| **Announcements** | System-wide announcements and changelogs |
| **Inquiries** | Inquiry and question submission |

### Shared Infrastructure

| Capability | Technology | Purpose |
|------------|------------|---------|
| Auditing | Audit.NET | Automatic entity change tracking via EF Core interceptor |
| Background Jobs | Hangfire | Deferred and recurring job execution via `IJobScheduler` |
| Workflows | Elsa 3 | Long-running, multi-step business processes |

## Tech Stack

| Purpose | Technology |
|---------|------------|
| Framework | .NET 10 |
| Database | PostgreSQL 18 |
| ORM | EF Core (writes) + Dapper (reads) |
| CQRS & Messaging | Wolverine + RabbitMQ |
| Caching | Valkey (Redis-compatible) |
| Identity | OpenIddict + ASP.NET Core Identity |
| Real-time | SignalR |
| Validation | FluentValidation |
| Logging | Serilog |
| Tracing | OpenTelemetry |
| Testing | xUnit, Testcontainers, FluentAssertions |

## Quick Start

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Docker](https://www.docker.com/get-started)

### 1. Start infrastructure

```bash
cd docker && docker compose up -d
```

This starts PostgreSQL, RabbitMQ, Mailpit, and Valkey.

### 2. Run the API

```bash
dotnet run --project src/Wallow.Api
```

### 3. Run tests

```bash
dotnet test                                                    # all tests
dotnet test tests/Modules/Billing/Wallow.Billing.Tests        # single module
```

## Key Features

- **Clean Architecture** -- Strict dependency rules per module with domain isolation
- **Domain-Driven Design** -- Entities, value objects, domain events, bounded contexts
- **CQRS** -- Command/query separation with Wolverine as mediator
- **Multi-Tenancy** -- Schema-per-tenant data isolation with shared infrastructure
- **Message-Based Communication** -- Events over RabbitMQ, never direct references
- **OpenIddict Identity** -- Authentication, RBAC, and user management
- **Real-Time** -- Push notifications via SignalR with Redis backplane
- **Observability** -- Serilog structured logging, OpenTelemetry tracing, Grafana dashboards
- **Audit Trail** -- Automatic entity change auditing via Audit.NET
- **Background Jobs** -- `IJobScheduler` abstraction backed by Hangfire
- **Workflows** -- Elsa 3 workflow engine for long-running processes

## Multi-Tenancy

Schema-per-tenant with shared infrastructure. Each tenant gets isolated database schemas while sharing the same PostgreSQL instance, RabbitMQ broker, and application process. Tenant resolution happens at the middleware layer via configurable strategies (header, subdomain, or JWT claim).

New tenant onboarding is config + migration, not a deployment.

## Scalability

Wallow is designed for horizontal scaling from day one:

| Strategy | How |
|----------|-----|
| **Horizontal scaling** | Stateless API behind a load balancer |
| **Distributed cache** | Valkey/Redis for shared state and SignalR backplane |
| **Competing consumers** | RabbitMQ enables parallel message processing across instances |
| **Durable outbox** | Wolverine's transactional outbox ensures reliable messaging |
| **Database scaling** | PgBouncer for connection pooling, read replicas for read-heavy workloads |
| **Worker separation** | Separate API instances from background workers |
| **Module extraction** | Extract high-load modules into independent services |

## Fork Workflow

1. **Fork** this repository
2. **Rename** namespaces from `Wallow.*` to `YourProduct.*`
3. **Add domain modules** following the established Clean Architecture pattern
4. **Configure tenants**
5. **Deploy** -- the platform ships as a single deployable unit

Upstream improvements to shared infrastructure can be pulled into forks.

## Local Services

| Service | URL | Credentials |
|---------|-----|-------------|
| API | http://localhost:5000 | -- |
| API Docs | http://localhost:5000/scalar/v1 | -- |

| RabbitMQ | http://localhost:15672 | See `docker/.env` |
| Mailpit | http://localhost:8025 | -- |
| Grafana | http://localhost:3001 | admin / admin |

## Documentation

| Doc | Description |
|-----|-------------|
| [Developer Guide](docs/getting-started/developer-guide.md) | How to work in the codebase |
| [Fork Guide](docs/getting-started/fork-guide.md) | Step-by-step guide for creating a new product |
| [Deployment Guide](docs/operations/deployment.md) | Server setup, CI/CD, and client app integration |
| [Architecture](docs/architecture/assessment.md) | Design decisions and architecture patterns |


## License

This project is licensed under the [MIT License](LICENSE).
