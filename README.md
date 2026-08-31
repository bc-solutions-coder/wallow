<div align="center">

<img src="assets/piggy-icon.svg" alt="Wallow" width="120" />

# Wallow

**A fork-first base platform for multi-tenant SaaS: a .NET modular monolith API plus a
TypeScript workspace of shared packages and React frontends.**

Fork it, add your domain modules, build your screens from the shared packages.

*Pre-release: Wallow has not been deployed outside local development yet, and breaking changes
to `main` are expected.*

[![CI](https://github.com/bc-solutions-coder/wallow/actions/workflows/ci.yml/badge.svg)](https://github.com/bc-solutions-coder/wallow/actions/workflows/ci.yml)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![PostgreSQL](https://img.shields.io/badge/PostgreSQL-18-4169E1?logo=postgresql&logoColor=white)](https://www.postgresql.org/)
[![License](https://img.shields.io/badge/license-Apache_2.0-green.svg)](LICENSE)

</div>

---

## What is Wallow?

Wallow is the infrastructure layer SaaS products keep rebuilding: identity and RBAC,
multi-tenant data isolation, notifications, announcements, file storage, API keys, and
per-client branding. You fork this repo, keep all of that, and write only your product's
modules and screens.

One repo, two toolchains:

- **`api/`** is a .NET 10 modular monolith. Seven modules, each an autonomous bounded context
  owning its own PostgreSQL schema and talking to the others only through integration events.
  The API is headless; the React apps are its only UIs. See [`api/README.md`](api/README.md).
- **`apps/` + `packages/`** are a pnpm workspace: a TypeScript SDK for same-origin OIDC (the
  BFF pattern), a shared component catalog, and three TanStack Start frontends.

Forks stay mergeable. `.gitattributes` merge drivers protect fork-owned config when you pull
upstream improvements, and rebranding is a single file, `packages/styles/branding.json`. No
source changes needed.

New here? Start with the [fork guide](docs/getting-started/fork-guide.md) or the
[developer guide](docs/getting-started/developer-guide.md).

## Repository layout

| Path | What it is |
|------|------------|
| `api/` | .NET 10 solution: the API, Aspire host, migrations, seeder, and the seven modules |
| `apps/` | TanStack Start frontends: `wallow-web` (dashboard), `wallow-auth` (login/MFA), `minimal-app` (external relying-party example) |
| `packages/` | Shared TypeScript packages: SDK, UI catalog, forms, auth, styles, and friends |
| `docker/` | Compose files for infra, production, and the E2E test stack |
| `docs/` | The DocFX documentation site |
| `scripts/` | Test runners (`run-tests.sh`, `e2e.sh`) and docs helpers |

## Quick start

You need the [.NET 10 SDK](https://dotnet.microsoft.com/download),
[Docker](https://www.docker.com/get-started), and [Node 24](https://nodejs.org/) with pnpm
(versions pinned in `.nvmrc` and `package.json`).

```bash
pnpm install                              # workspace dependencies
pnpm backend:infra                        # Postgres, Valkey, GarageHQ, Mailpit, Grafana
pnpm backend                              # Aspire AppHost: API + both React apps + migration + seeder
```

That's the whole stack. To run pieces individually:

```bash
dotnet run --project api/src/Wallow.Api                       # API   → http://localhost:5001
pnpm --filter @bc-solutions-coder/wallow-web dev              # Web   → http://localhost:3000
pnpm --filter @bc-solutions-coder/wallow-auth dev             # Auth  → http://localhost:3002
```

## Testing and quality gates

```bash
./scripts/run-tests.sh                    # backend fast suites (integration excluded)
./scripts/run-tests.sh all                # the same plus the integration suites (needs Docker)
pnpm check                                # frontend gate: format, lint, build, typecheck, test
./scripts/e2e.sh                          # containerised backend + all three Playwright suites
```

Backend detail (tiers, coverage, the integration category) is in
[`api/README.md`](api/README.md); the full picture is in the
[testing guide](docs/development/testing.md).

## Local services

| Service | URL |
|---------|-----|
| API | http://localhost:5001 |
| API docs (Scalar) | http://localhost:5001/scalar/v1 |
| Web (TanStack) | http://localhost:3000 |
| Auth (TanStack) | http://localhost:3002 |
| Minimal app | http://localhost:3010 |
| Docs | http://localhost:5004 |
| Mailpit | http://localhost:8025 |
| GarageHQ (S3) | http://localhost:3900 |
| Grafana | http://localhost:3001 |

Credentials and config: [configuration guide](docs/getting-started/configuration.md). The
application rows are duplicated in root `CLAUDE.md`'s Local Development table. Change both
together.

## Documentation

| Guide | Description |
|-------|-------------|
| [Developer guide](docs/getting-started/developer-guide.md) | Day-to-day development workflow |
| [Fork guide](docs/getting-started/fork-guide.md) | Creating a new product from Wallow |
| [Configuration](docs/getting-started/configuration.md) | Environment variables, branding, settings |
| [Architecture](docs/architecture/assessment.md) | Design decisions and patterns |
| [Module creation](docs/architecture/module-creation.md) | Adding new modules |
| [Deployment](docs/operations/deployment.md) | CI/CD, Docker, and production setup |
| [Versioning](docs/operations/versioning.md) | Conventional Commits and release-please |
| [Observability](docs/operations/observability.md) | Logging, tracing, and dashboards |
| [Frontend setup](docs/development/frontend-setup.md) | The pnpm workspace, Vite, and TanStack Start |
| [Component library](docs/development/component-library.md) | The shared `@bc-solutions-coder/ui` catalog |
| [Forms](docs/development/forms.md) | The `@bc-solutions-coder/forms` authoring layer |
| [Frontend state](docs/development/frontend-state.md) | TanStack Query, auth, and the nav store |
| [Logging](docs/development/logging.md) | Structured logging across the browser and the app server |
| [BFF pattern](docs/integrations/bff-pattern.md) | Same-origin OIDC through the app server |
| [TypeScript SDK](docs/integrations/typescript-sdk.md) | `@bc-solutions-coder/sdk` reference |

## License

[Apache 2.0](LICENSE)
