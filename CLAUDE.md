# CLAUDE.md

Foundry is a .NET 10 modular monolith with multi-tenancy, Clean Architecture, DDD, CQRS, and RabbitMQ messaging. Teams fork and extend this as a base platform.

## Commands

```bash
# Start infrastructure (Postgres, RabbitMQ, Mailpit, Valkey, Keycloak)
cd docker && docker compose up -d

# Run the API
dotnet run --project src/Foundry.Api

# Run all tests
dotnet test

# Run specific module tests
dotnet test tests/Modules/Billing/Billing.Domain.Tests

# EF Core migrations
dotnet ef migrations add MigrationName \
    --project src/Modules/{Module}/Foundry.{Module}.Infrastructure \
    --startup-project src/Foundry.Api \
    --context {Module}DbContext
```

## Architecture

**Modules:** Identity, Billing, Storage, Notifications, Messaging, Announcements, Inquiries, Showcases

- Modules communicate via RabbitMQ events, never direct references
- Modules only reference `Shared.Contracts` for cross-module communication
- Each module owns its database schema (separate PostgreSQL schemas)
- Clean Architecture per module: Domain -> Application -> Infrastructure -> Api
- Domain has no external dependencies; Application depends only on Domain
- EF Core for writes, Dapper for complex reads
- Wolverine auto-discovers handlers in all `Foundry.*` assemblies
- Package versions centrally managed in `Directory.Packages.props`

## Versioning

Automated semver via [Conventional Commits](https://www.conventionalcommits.org/) + [release-please](https://github.com/googleapis/release-please). See `docs/VERSIONING_GUIDE.md`.

**Commit message format:** `<type>[optional scope][!]: <description>`

| Prefix | Bump | Example |
|--------|------|---------|
| `fix:` | Patch | `fix: resolve null ref in tenant resolver` |
| `feat:` | Minor | `feat: add billing invoice export` |
| `feat!:` | Major | `feat!: redesign authentication API` |
| `chore:`, `refactor:`, `docs:`, `test:`, `ci:` | No release | `chore: update dependencies` |

- Merges to main create/update a **Release PR** with changelog and version bump
- Merging the Release PR tags, creates a GitHub Release, and triggers Docker image publish

## Local Development

| Service | URL | Credentials |
|---------|-----|-------------|
| API | http://localhost:5000 | N/A |
| Keycloak Admin | http://localhost:8080 | See `docker/.env` |
| Keycloak Realm | foundry | admin@foundry.dev / Admin123! |
| RabbitMQ | http://localhost:15672 | See `docker/.env` |
| Mailpit | http://localhost:8025 | N/A |
| Grafana | http://localhost:3000 | admin / admin |

## Documentation

- **Module creation guide:** `docs/claude/module-creation.md`
- **Module simplification:** `docs/claude/module-simplification.md`
- **Developer guide:** `docs/DEVELOPER_GUIDE.md`
- **Deployment & CI/CD:** `docs/DEPLOYMENT_GUIDE.md`
- **Versioning guide:** `docs/VERSIONING_GUIDE.md`

## Agent Instructions

Uses **bd** (beads) for issue tracking.

```bash
bd ready                                    # Find available work
bd show <id>                                # View issue details
bd update <id> --status in_progress         # Claim work
bd close <id>                               # Complete work
bd sync                                     # Sync with git
```

### Session Completion

Work is NOT complete until `git push` succeeds.

1. File issues for remaining work
2. Run quality gates (if code changed)
3. Close finished issues, update in-progress items
4. `git pull --rebase && bd sync && git push`
5. Verify `git status` shows "up to date with origin"
