# CLAUDE.md

Wallow is a .NET 10 modular monolith with multi-tenancy, Clean Architecture, DDD, CQRS, and Wolverine in-memory messaging. Teams fork and extend this as a base platform.

## Commands

```bash
# Start infrastructure (Postgres, Valkey, GarageHQ, Mailpit, Grafana)
cd docker && docker compose up -d

# Start infrastructure with ClamAV virus scanning
cd docker && docker compose --profile clamav up -d

# Run the API
dotnet run --project src/Wallow.Api

# Run all tests (structured output with per-assembly results)
./scripts/run-tests.sh

# Run specific module tests
./scripts/run-tests.sh billing
./scripts/run-tests.sh identity
# Supported: identity, billing, storage, notifications, messaging, announcements,
#            inquiries, branding, apikeys, auth, api, arch, shared, kernel, integration

# EF Core migrations
dotnet ef migrations add MigrationName \
    --project src/Modules/{Module}/Wallow.{Module}.Infrastructure \
    --startup-project src/Wallow.Api \
    --context {Module}DbContext
```

## Architecture

**Modules:** Identity, Billing, Storage, Notifications, Messaging, Announcements, Inquiries

- Modules communicate via Wolverine in-memory events, never direct references
- Modules only reference `Shared.Contracts` for cross-module communication
- Each module owns its database schema (separate PostgreSQL schemas)
- Clean Architecture per module: Domain -> Application -> Infrastructure -> Api
- Domain has no external dependencies; Application depends only on Domain
- EF Core for writes, Dapper for complex reads
- Wolverine auto-discovers handlers in all `Wallow.*` assemblies
- Package versions centrally managed in `Directory.Packages.props`

## Fork-First Architecture

- **Two UI boundaries:** `Wallow.Auth` (login, register, password reset) and `Wallow.Web` (dashboard, public pages) — each is a separate Blazor app
- **BrandingOptions pattern:** `Wallow.Auth` owns the canonical `BrandingOptions` class; `Wallow.Web` has a local copy. Both read from `branding.json` at the repo root
- **Config-driven customization:** Forks customize identity via `branding.json` (name, icon, tagline, theme colors) and `appsettings.json` — no source code changes required for rebranding
- **Merge driver:** `.gitattributes` marks `branding.json`, `appsettings*.json`, `docker/.env`, and `CLAUDE.md` as `merge=ours` so upstream merges preserve fork config

## Versioning

Automated semver via [Conventional Commits](https://www.conventionalcommits.org/) + [release-please](https://github.com/googleapis/release-please). See `docs/operations/versioning.md`.

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
| Docs | http://localhost:5004 | N/A |
| GarageHQ (S3) | http://localhost:3900 | See `docker/.env` |
| Mailpit | http://localhost:8025 | N/A |
| Grafana | http://localhost:3001 | admin / admin |

## Documentation

- **Fork guide:** `docs/getting-started/fork-guide.md`
- **Configuration guide:** `docs/getting-started/configuration.md`
- **Frontend setup:** `docs/development/frontend-setup.md`
- **Module creation guide:** `.claude/docs/module-creation.md`
- **Module simplification:** `.claude/docs/module-simplification.md`
- **Developer guide:** `docs/getting-started/developer-guide.md`
- **Deployment & CI/CD:** `docs/operations/deployment.md`
- **Versioning guide:** `docs/operations/versioning.md`

## Agent Instructions

Uses **bd** (beads) for issue tracking.

```bash
bd ready                                    # Find available work
bd show <id>                                # View issue details
bd update <id> --status in_progress         # Claim work
bd close <id>                               # Complete work
```

### Session Completion

Work is NOT complete until `git push` succeeds.

1. File issues for remaining work
2. Run quality gates (if code changed)
3. Close finished issues, update in-progress items
4. `git pull --rebase && bd dolt push && git push`
5. Verify `git status` shows "up to date with origin"
