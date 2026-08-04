# Wallow Platform

Wallow is a .NET 10 modular monolith starter platform with multi-tenancy, Clean Architecture, DDD, CQRS, and Wolverine in-memory messaging, plus a TypeScript BFF SDK and a set of shared frontend packages for building same-origin OIDC frontends. Teams fork and extend this as a base platform.

Each section below is a curated starting point, not a full index — use the sidebar for the complete table of contents.

## Getting Started

- [Fork Guide](getting-started/fork-guide.md) — how to fork and customize Wallow for your team
- [Developer Guide](getting-started/developer-guide.md) — local setup and development workflow
- [Onboarding](getting-started/onboarding.md) — zero to productive in 30 minutes
- [Configuration](getting-started/configuration.md) — environment and runtime configuration

## Architecture

- [Module Creation](architecture/module-creation.md) — adding new modules
- [Messaging](architecture/messaging.md) — Wolverine event patterns
- [Architecture Assessment](architecture/assessment.md) — a point-in-time review of the shipped architecture

## Development

- [API Development](development/api-development.md) — controller patterns and error handling
- [Database Development](development/database-development.md) — EF Core patterns and query conventions
- [Testing](development/testing.md) — test practices and conventions
- [Frontend Setup](development/frontend-setup.md) — the TanStack Start apps and the shared packages they build on
- [Component Library](development/component-library.md) — the `@bc-solutions-coder/ui` catalog

## Integrations

- [BFF Pattern](integrations/bff-pattern.md) — same-origin OIDC through the app server
- [TypeScript SDK](integrations/typescript-sdk.md) — `@bc-solutions-coder/sdk` auth and API client
- [Integration Cookbook](integrations/integration-cookbook.md) — wiring the SDK into an existing app

## Operations

- [Deployment](operations/deployment.md) — CI/CD and infrastructure
- [Versioning](operations/versioning.md) — semver via Conventional Commits
- [Observability](operations/observability.md) — logging, metrics, tracing
