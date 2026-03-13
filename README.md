<div align="center">

# Foundry

**A production-ready .NET modular monolith for building multi-tenant SaaS products.**

Fork it. Add your domain modules. Deploy.

<!-- Shields: replace with your own URLs or remove any that don't apply -->
[![CI](https://github.com/bc-solutions-coder/Foundry/actions/workflows/ci.yml/badge.svg)](https://github.com/bc-solutions-coder/Foundry/actions/workflows/ci.yml)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![PostgreSQL](https://img.shields.io/badge/PostgreSQL-18-4169E1?logo=postgresql&logoColor=white)](https://www.postgresql.org/)
[![License](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE)
[![Docker](https://img.shields.io/badge/Docker-ready-2496ED?logo=docker&logoColor=white)](docker/)
[![Keycloak](https://img.shields.io/badge/Keycloak-26-4D4D4D?logo=keycloak&logoColor=white)](https://www.keycloak.org/)

</div>

---

## What is Foundry?

Foundry provides the cross-cutting infrastructure every SaaS product needs out of the box: identity management, billing, notifications, messaging, file storage, and multi-tenant data isolation. You write the business logic.

New products are created by forking Foundry and adding domain-specific modules. The base platform stays generic -- improvements to shared infrastructure can be pulled from upstream into forks.

## Architecture

Foundry is a **modular monolith**. Each module is an autonomous bounded context that follows Clean Architecture internally.

```
src/
├── Foundry.Api/                  # Host, middleware, routing
├── Modules/
│   ├── Identity/                 # Auth, users, organizations, RBAC
│   ├── Billing/                  # Payments, invoices, subscriptions
│   ├── Storage/                  # File storage (S3, local)
│   ├── Notifications/            # In-app and push notifications
│   ├── Messaging/                # User-to-user conversations
│   ├── Announcements/            # System-wide announcements
│   ├── Inquiries/                # Inquiry and question submission
│   └── Showcases/                # Portfolio and showcase items
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
| **Identity** | Authentication, users, organizations, roles, RBAC via Keycloak 26 |
| **Billing** | Payments, invoices, subscription lifecycle |
| **Storage** | File storage abstraction (S3-compatible, local filesystem) |
| **Notifications** | In-app and push notifications, delivery preferences |
| **Messaging** | User-to-user conversations and threads |
| **Announcements** | System-wide announcements and changelogs |
| **Inquiries** | Inquiry and question submission |
| **Showcases** | Portfolio and showcase items |

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
| Identity | Keycloak 26 |
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

This starts PostgreSQL, RabbitMQ, Mailpit, Valkey, and Keycloak.

### 2. Run the API

```bash
dotnet run --project src/Foundry.Api
```

### 3. Run tests

```bash
dotnet test                                                    # all tests
dotnet test tests/Modules/Billing/Foundry.Billing.Tests        # single module
```

## Key Features

- **Clean Architecture** -- Strict dependency rules per module with domain isolation
- **Domain-Driven Design** -- Entities, value objects, domain events, bounded contexts
- **CQRS** -- Command/query separation with Wolverine as mediator
- **Multi-Tenancy** -- Schema-per-tenant data isolation with shared infrastructure
- **Message-Based Communication** -- Events over RabbitMQ, never direct references
- **Keycloak Identity** -- Authentication, RBAC, and user management
- **Real-Time** -- Push notifications via SignalR with Redis backplane
- **Observability** -- Serilog structured logging, OpenTelemetry tracing, Grafana dashboards
- **Audit Trail** -- Automatic entity change auditing via Audit.NET
- **Background Jobs** -- `IJobScheduler` abstraction backed by Hangfire
- **Workflows** -- Elsa 3 workflow engine for long-running processes

## Multi-Tenancy

Schema-per-tenant with shared infrastructure. Each tenant gets isolated database schemas while sharing the same PostgreSQL instance, RabbitMQ broker, and application process. Tenant resolution happens at the middleware layer via configurable strategies (header, subdomain, or JWT claim).

New tenant onboarding is config + migration, not a deployment.

## Scalability

Foundry is designed for horizontal scaling from day one:

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
2. **Rename** namespaces from `Foundry.*` to `YourProduct.*`
3. **Add domain modules** following the established Clean Architecture pattern
4. **Configure tenants**
5. **Deploy** -- the platform ships as a single deployable unit

Upstream improvements to shared infrastructure can be pulled into forks.

## Local Services

| Service | URL | Credentials |
|---------|-----|-------------|
| API | http://localhost:5000 | -- |
| API Docs | http://localhost:5000/scalar/v1 | -- |
| Keycloak Admin | http://localhost:8080 | See `docker/.env` |
| RabbitMQ | http://localhost:15672 | See `docker/.env` |
| Mailpit | http://localhost:8025 | -- |
| Grafana | http://localhost:3000 | admin / admin |

## Documentation

| Doc | Description |
|-----|-------------|
| [Developer Guide](docs/DEVELOPER_GUIDE.md) | How to work in the codebase |
| [Forking Guide](docs/FORKING_GUIDE.md) | Step-by-step guide for creating a new product |
| [Deployment Guide](docs/DEPLOYMENT_GUIDE.md) | Server setup, CI/CD, and client app integration |
| [Deployment Strategies](docs/deployment-strategies/) | Horizontal scaling, database scaling, worker separation, module extraction |
| [Architecture Reference](docs/FOUNDRY.md) | Single architecture and design reference |
| [Keycloak Integration](docs/plans/2026-02-05-keycloak-integration-design.md) | Identity provider setup |

## License

This project is licensed under the [MIT License](LICENSE).
