<div align="center">

<img src="assets/piggy-icon.svg" alt="Wallow" width="120" />

# Wallow

**A production-ready .NET modular monolith for building multi-tenant SaaS products.**

Fork it. Add your domain modules. Deploy.

[![CI](https://github.com/bc-solutions-coder/Wallow/actions/workflows/ci.yml/badge.svg)](https://github.com/bc-solutions-coder/Wallow/actions/workflows/ci.yml)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![PostgreSQL](https://img.shields.io/badge/PostgreSQL-18-4169E1?logo=postgresql&logoColor=white)](https://www.postgresql.org/)
[![Tests](https://img.shields.io/badge/tests-6%2C078_passing-brightgreen?logo=checkmarx&logoColor=white)](#testing)
[![Coverage](https://img.shields.io/badge/coverage-97.7%25_lines_·_89.5%25_branches-brightgreen?logo=codecov&logoColor=white)](#testing)
[![License](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE)
[![Docker](https://img.shields.io/badge/Docker-ready-2496ED?logo=docker&logoColor=white)](docker/)

</div>

---

## What is Wallow?

Wallow provides the cross-cutting infrastructure every SaaS product needs out of the box -- identity, billing, notifications, messaging, file storage, and multi-tenant data isolation. You write the business logic.

The intended workflow is to **fork this repo and build your product on top**. Shared infrastructure improvements can be pulled from upstream into forks without conflicts.

> **New here?** Start with the [Fork Guide](docs/getting-started/fork-guide.md) or the [Developer Guide](docs/getting-started/developer-guide.md).

## Quick Start

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Docker](https://www.docker.com/get-started)

### 1. Start infrastructure

```bash
cd docker && docker compose up -d
```

Starts PostgreSQL, Valkey, GarageHQ (S3), Mailpit, and Grafana.

### 2. Run the apps

```bash
dotnet run --project src/Wallow.Api       # API        → http://localhost:5001
dotnet run --project src/Wallow.Auth      # Auth UI    → http://localhost:5002
dotnet run --project src/Wallow.Web       # Web UI     → http://localhost:5003
```

### 3. Run tests

```bash
./scripts/run-tests.sh                    # all tests
./scripts/run-tests.sh billing            # single module
```

> See [Testing](docs/development/testing.md) for coverage, E2E, and CI details.

## Architecture

A **modular monolith** where each module is an autonomous bounded context following Clean Architecture. Modules communicate through Wolverine in-memory events via `Shared.Contracts` -- never direct references. Each module owns its own PostgreSQL schema.

```
src/
├── Wallow.Api/                  # Host, middleware, routing
├── Wallow.Auth/                 # Blazor: login, register, password reset
├── Wallow.Web/                  # Blazor: dashboard and public pages
├── Modules/
│   ├── Identity/                # Auth, users, organizations, RBAC
│   ├── Billing/                 # Payments, invoices, subscriptions
│   ├── Storage/                 # File storage (S3-compatible)
│   ├── Notifications/           # In-app and push notifications
│   ├── Messaging/               # User-to-user conversations
│   ├── Announcements/           # System-wide announcements
│   ├── Inquiries/               # Inquiry and question submission
│   ├── ApiKeys/                 # API key management
│   └── Branding/                # Tenant branding configuration
└── Shared/
    ├── Contracts/               # Cross-module event definitions
    └── Kernel/                  # Base classes, shared abstractions
```

Each module follows four layers: **Domain** (no dependencies) → **Application** → **Infrastructure** → **API**.

> Deep dive: [Architecture Assessment](docs/architecture/assessment.md) · [Module Creation](docs/architecture/module-creation.md)

## Key Features

| Feature | Description |
|---------|-------------|
| **Clean Architecture** | Strict dependency rules per module with domain isolation |
| **Domain-Driven Design** | Entities, value objects, domain events, bounded contexts |
| **CQRS** | Command/query separation with Wolverine as mediator |
| **Multi-Tenancy** | Schema-per-tenant data isolation, configurable resolution (header, subdomain, JWT) |
| **Event-Driven** | Wolverine in-memory events between modules |
| **Identity & RBAC** | OpenIddict + ASP.NET Core Identity |
| **Real-Time** | Push notifications via SignalR |
| **Observability** | Serilog structured logging, OpenTelemetry tracing, [Grafana dashboards](docs/operations/observability.md) |
| **Audit Trail** | Automatic entity change auditing via Audit.NET |
| **Background Jobs** | `IJobScheduler` abstraction backed by Hangfire |
| **Workflows** | Elsa 3 engine for long-running business processes |

## Tech Stack

| Purpose | Technology |
|---------|------------|
| Framework | .NET 10 |
| Database | PostgreSQL 18 |
| ORM | EF Core + Dapper (available for raw SQL reads) |
| CQRS & Messaging | Wolverine (in-memory) |
| Caching | Valkey (Redis-compatible) |
| Identity | OpenIddict + ASP.NET Core Identity |
| Real-time | SignalR |
| Validation | FluentValidation |
| Logging & Tracing | Serilog, OpenTelemetry |
| Testing | xUnit, Testcontainers, AwesomeAssertions |

## Testing

6,078 tests across 45 assemblies, all passing.

| Metric | Coverage |
|--------|----------|
| Lines | **97.7%** (13,457 / 13,771) |
| Branches | **89.5%** (2,235 / 2,497) |
| Methods | **96.9%** (1,735 / 1,789) |

> Details: [Testing Guide](docs/development/testing.md) · [Coverage](docs/development/testing-coverage.md) · [E2E Tests](docs/development/testing-e2e.md) · [CI](docs/development/testing-ci.md)

## Configuration

Wallow is designed to be customized without changing source code. All configuration flows through standard .NET mechanisms:

| Area | Config Source | What it controls |
|------|-------------|------------------|
| **Branding** | `branding.json` | App name, icon, tagline, theme colors |
| **Database** | `appsettings.json` | PostgreSQL and Valkey connection strings |
| **Email** | `appsettings.json` | SMTP host, port, TLS, sender defaults |
| **Storage** | `appsettings.json` | S3 endpoint, bucket, ClamAV virus scanning |
| **Observability** | `appsettings.json` | OpenTelemetry OTLP endpoints, service name |
| **CORS** | `appsettings.json` | Allowed origins for API requests |
| **Environment** | Environment variables | Override any setting with `Section__Key` syntax |

Configuration loads in order: `appsettings.json` → `appsettings.{Environment}.json` → environment variables → user secrets (dev only).

> Full reference with examples for Docker, Kubernetes, and all module options: [Configuration Guide](docs/getting-started/configuration.md)

## Local Services

| Service | URL |
|---------|-----|
| API | http://localhost:5001 |
| API Docs (Scalar) | http://localhost:5001/scalar/v1 |
| Auth UI | http://localhost:5002 |
| Web UI | http://localhost:5003 |
| Docs | http://localhost:5004 |
| Mailpit | http://localhost:8025 |
| GarageHQ (S3) | http://localhost:3900 |
| Grafana | http://localhost:3001 |

> Credentials and config: [Configuration Guide](docs/getting-started/configuration.md)

## Documentation

| Guide | Description |
|-------|-------------|
| [Developer Guide](docs/getting-started/developer-guide.md) | Day-to-day development workflow |
| [Fork Guide](docs/getting-started/fork-guide.md) | Creating a new product from Wallow |
| [Configuration](docs/getting-started/configuration.md) | Environment variables, branding, settings |
| [Architecture](docs/architecture/assessment.md) | Design decisions and patterns |
| [Module Creation](docs/architecture/module-creation.md) | Adding new modules |
| [Deployment](docs/operations/deployment.md) | CI/CD, Docker, and production setup |
| [Versioning](docs/operations/versioning.md) | Conventional Commits and release-please |
| [Observability](docs/operations/observability.md) | Logging, tracing, and dashboards |

## License

[MIT](LICENSE)
