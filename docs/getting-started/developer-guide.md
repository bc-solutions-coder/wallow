# Wallow Developer Guide

---

## Prerequisites

- .NET 10 SDK
- Node 24 (see `.nvmrc`) and pnpm 11.24.0, for the frontend workspace
- Docker and Docker Compose
- Rider or Visual Studio 2022+

---

## Getting Started

### 1. Create the Docker environment file

Docker Compose reads `docker/.env`, which is not committed. Copy the example and fill in your own values:

```bash
cp docker/.env.example docker/.env
```

`GF_ADMIN_PASSWORD` is **required** -- Compose refuses to start the Grafana LGTM service when it is unset.

### 2. Start Infrastructure

Wallow depends on PostgreSQL, Valkey (Redis-compatible cache), GarageHQ (S3-compatible object storage), and Mailpit. Docker Compose provisions all of them:

```bash
pnpm backend:infra          # = cd docker && docker compose up -d
pnpm backend:infra:down     # stop them again
```

To also start ClamAV for virus scanning on file uploads (optional), use Compose directly -- there is no pnpm wrapper for the profile:

```bash
cd docker && docker compose --profile clamav up -d
```

Authentication is handled by the embedded OpenIddict server (part of the Identity module), so no external identity provider container is needed.

### 3. Run the API

```bash
pnpm backend
```

`pnpm backend` runs the .NET Aspire host (`Wallow.AppHost`), which orchestrates the infrastructure containers, the `wallow-migrations` project resource, the seeder, the API and both React apps. It is the canonical way to run Wallow locally, and step 2 is only needed when you want the containers without Aspire.

The API starts on `http://localhost:5001`. Interactive API documentation is available at `http://localhost:5001/scalar/v1`.

You can still run the API alone with `dotnet run --project api/src/Wallow.Api` against `pnpm backend:infra`, but **nothing migrates on API startup** -- you must run `Wallow.MigrationService` or `dotnet ef database update` yourself first. See [Database Migrations](../development/database-migrations.md).

### 4. Run Tests

```bash
# All tests
./scripts/run-tests.sh

# Specific module
./scripts/run-tests.sh identity

# Specific test project
./scripts/run-tests.sh api/tests/Modules/Inquiries/Wallow.Inquiries.Tests
```

The script outputs structured per-assembly pass/fail counts and lists individual failed test names. Supported shorthands, as defined in `resolve_filter()` in `scripts/run-tests.sh`: `identity`, `storage`, `notifications`, `announcements`, `inquiries`, `branding`, `apikeys`, `api`, `arch` (or `architecture`), `seeder`, `migrations`, `shared`, `kernel`, `integration`, `all`. Matching is case-insensitive; anything else is passed through to `dotnet test` as a project path.

Integration tests require Docker. Testcontainers spins up ephemeral Postgres and Valkey containers automatically. They are excluded from a normal run, because every argument other than `integration` and `all` appends `--filter "Category!=E2E&Category!=Integration"`:

```bash
./scripts/run-tests.sh integration   # ONLY Category=Integration, across the whole solution
./scripts/run-tests.sh all           # the fast suites and the integration suites together
```

A second argument narrows the tier to the first argument's target -- `./scripts/run-tests.sh api integration` runs only `Wallow.Api.Tests`'s integration tests, and anything other than `integration` or `all` there exits 2 rather than being ignored.

Both select by category over `api/Wallow.slnx` rather than by project, because integration tests live in seven assemblies -- `Wallow.Api.Tests` among them, whose `HandlerCodegenTests` is the only guard that every discovered Wolverine handler compiles. A run that excludes the tier prints `SCOPE: fast suites only` beside its totals and ends with an `INTEGRATION TESTS DID NOT RUN` banner, so a green total is never mistakable for full coverage.

### 5. Run the Frontend

The API is headless. The user interfaces are two TanStack Start React apps in the pnpm workspace: `apps/wallow-web` (dashboard) and `apps/wallow-auth` (login, signup, MFA).

The workspace targets **Node 24** (pinned in `.nvmrc`) and **pnpm 11.24.0** (pinned as `packageManager` in `package.json`). Install the workspace once:

```bash
pnpm install
```

Then start everything -- infrastructure, the API, and both React apps -- through the .NET Aspire AppHost, which runs the frontends as Node resources:

```bash
pnpm backend
```

| App | URL | Package |
|-----|-----|---------|
| Web (dashboard) | http://localhost:3000 | `apps/wallow-web` |
| Auth (login/signup/MFA) | http://localhost:3002 | `apps/wallow-auth` |

Both ports can be overridden with `PORT`. For running an app on its own, the shared SDK build order, and the same-origin BFF setup, see the [Frontend Setup guide](../development/frontend-setup.md).

The workspace's quality gate is `pnpm check`: format check, both lint passes, manifest/dependency/env checks, then `turbo run build typecheck test` and `check:exports`. It is what `js.yml` runs in CI, so run it before opening a PR that touches `apps/` or `packages/`. Turbo caches build, typecheck and test locally in `.turbo/`, so a warm run is far faster than the first.

Workflow YAML has a separate gate: `pnpm lint:actions` runs actionlint over `.github/workflows`, preferring an `actionlint` on your PATH and falling back to a pinned docker image. It stays out of `pnpm check` so `check` remains runnable offline; CI covers it with `.github/workflows/actionlint.yml`, path-filtered to `.github/**`. Run it before pushing a workflow edit.

#### Turbo remote cache

Turbo can additionally share artifacts with a self-hosted remote cache, so a laptop build and a
CI run reuse each other's work. It activates only when three environment variables are set —
without them turbo is silently local-only, and a failing remote is a warning, never a red run:

| Variable | Value |
|----------|-------|
| `TURBO_API` | The cache server's origin (turbo appends `/v8/artifacts/...` itself) |
| `TURBO_TEAM` | The team slug that namespaces artifacts on the server |
| `TURBO_TOKEN` | The bearer token the cache server checks |

CI reads all three from GitHub Actions **secrets** of the same names (the URL is a secret too —
the hostname stays out of the repo). Fork PRs receive no secrets and run uncached, which is
correct. Locally, export the three values in your shell; get them from the password manager or
from whoever operates the cache server.

Three workflows are on the cache: `js.yml` (the `pnpm check` gate), `route-tree-drift.yml` (it
builds the three apps through `turbo run build` rather than raw `pnpm --filter` builds) and
`package-publish.yml` (its build and test run through turbo from the repo root, not from the
package directory). All three use the same env block and Tailscale steps.

The cache server is not on the public internet: `TURBO_API` is its **tailnet** address. CI joins
the tailnet as a tagged ephemeral node via the Tailscale GitHub Action (secrets
`TS_OAUTH_CLIENT_ID`/`TS_OAUTH_SECRET`, tag `tag:ci`), and a laptop must be on the same tailnet
for the address to resolve — off the tailnet, turbo simply warns and runs local-only.

Turbo authenticates with `Authorization: Bearer $TURBO_TOKEN` and nothing else — it cannot send
custom headers or query parameters — so the cache server's own bearer check is the sole
credential, and network reachability is governed by tailnet ACLs rather than by a proxy in front
of the server.

When the cache misbehaves: `turbo run <task> --force` re-executes everything while still writing
results back, `TURBO_REMOTE_CACHE_READ_ONLY=true` stops uploads, and unsetting `TURBO_TOKEN`
disconnects the remote entirely.

### Local Services

| Service | URL | Credentials |
|---------|-----|-------------|
| API | http://localhost:5001 | - |
| Scalar Docs | http://localhost:5001/scalar/v1 | - |
| OpenIddict Authorize | http://localhost:5001/connect/authorize | - |
| OpenIddict Token | http://localhost:5001/connect/token | - |
| Hangfire Dashboard | http://localhost:5001/hangfire | - |
| Web app | http://localhost:3000 | - |
| Auth app | http://localhost:3002 | - |
| Docs site (DocFX) | http://localhost:5004 | Started separately: `./scripts/docs-serve.sh` |
| GarageHQ (S3 API) | http://localhost:3900 | See `docker/.env` |
| GarageHQ (Admin API) | http://localhost:3903 | See `docker/.env` |
| Mailpit | http://localhost:8025 | - |
| PostgreSQL | localhost:5432 | See `docker/.env` |
| Valkey | localhost:6379 | See `docker/.env` |
| AsyncAPI Viewer | http://localhost:5001/asyncapi | Dev only |
| ClamAV (optional) | localhost:3310 | - |
| Grafana | http://localhost:3001 | `admin` / `GF_ADMIN_PASSWORD` from `docker/.env` |

### Getting a Test Token

Use the OpenIddict token endpoint with client credentials or authorization code flow:

```bash
# Client credentials (service account)
curl -s -X POST http://localhost:5001/connect/token \
  -d "grant_type=client_credentials" \
  -d "client_id=<your-client-id>" \
  -d "client_secret=<your-client-secret>" \
  -d "scope=openid profile email"
```

In development, the `ApiScopeSeeder` seeds default API scopes at startup. Use the `ClientsController` admin API (`/v1/identity/clients`) to register new OpenIddict applications and obtain client credentials.

### Resetting Infrastructure

`pnpm backend:infra:down` stops the containers but keeps their volumes. To wipe the data too, drop to Compose for the `-v` flag, then bring everything back up through Aspire so migrations and seeding run again:

```bash
cd docker && docker compose down -v && cd ..
pnpm backend
```

---

## Issue Tracking

Wallow tracks work with **GitHub Issues** on this repository, driven through the
[`gh` CLI](https://cli.github.com/):

```bash
gh issue list --state open                   # Find available work
gh issue view <number> --comments            # View issue details
gh issue edit <number> --add-assignee @me    # Claim work
gh issue comment <number> --body "..."       # Record a finding on the issue
gh issue close <number> --comment "..."      # Complete work
```

`docs/agents/issue-tracker.md` is the canonical description of the conventions, and
`docs/agents/triage-labels.md` maps the triage label vocabulary. `gh` infers the repository from
`git remote -v`, so a fresh machine needs nothing beyond `gh auth login`.

### The beads archive

Until August 2026 Wallow tracked work with [beads](https://github.com/steveyegge/beads) (`bd`),
whose issue IDs (`Wallow-xxxx`) still appear throughout code comments, commit messages, and
`docs/plans/`. Those references are historical provenance, like ticket numbers from any retired
tracker. The full export — every issue with its notes, plus the agent memories — lives in
`docs/agents/beads-archive/` (`issues.json`, `memories.json`), so a `Wallow-xxxx` citation can
always be resolved from a fresh clone without the `bd` tool. The retired Dolt data also still
sits on the remote's `refs/dolt/data` ref; nothing reads it anymore.

---

## Architecture Overview

Wallow is a modular monolith. Each module is an autonomous bounded context that follows Clean Architecture internally and communicates with other modules exclusively through integration events over Wolverine. Modules never reference each other directly.

**Modules:** Identity, Branding, Storage, Notifications, Announcements, Inquiries, ApiKeys

**Shared libraries:**
- `Wallow.Shared.Contracts` -- Cross-module integration events and DTOs
- `Wallow.Shared.Kernel` -- Base classes, multi-tenancy primitives, shared abstractions
- `Wallow.Shared.Api` -- Shared API extensions, settings, and health checks
- `Wallow.Shared.Infrastructure` -- Settings and AsyncAPI document generation
- `Wallow.Shared.Infrastructure.Core` -- Auditing, caching, messaging, persistence, middleware
- `Wallow.Shared.Infrastructure.BackgroundJobs` -- `IJobScheduler` over Hangfire
- `Wallow.Shared.Infrastructure.Plugins` -- Plugin loading and lifecycle

---

## Shared Infrastructure

Cross-cutting capabilities that were previously separate modules now live in `Wallow.Shared.Infrastructure` and are registered centrally. Modules access these through DI -- they don't reference other modules.

### Auditing (`Shared.Infrastructure.Core/Auditing/`)

Authentication and account-lifecycle events are recorded as append-only rows in a dedicated `auth_audit` schema via `AuthAuditDbContext`. Wolverine event handlers translate domain events (logins, lockouts, membership transitions, client lifecycle changes) into `AuthAuditRecord`s through `IAuthAuditService`; the catalogue of event types lives in [Audit events](../operations/audit-events.md). There is no automatic entity-change interceptor — a change worth auditing raises an explicit event.

**Registration:** `services.AddAuthAuditing(configuration)` registers the `AuthAuditDbContext` factory and `IAuthAuditService`.

### Background Jobs (`Shared.Infrastructure.BackgroundJobs/`)

A thin `IJobScheduler` abstraction (defined in `Shared.Kernel/BackgroundJobs/`) over Hangfire for fire-and-forget and recurring jobs. Modules inject `IJobScheduler` to enqueue work without depending on Hangfire directly.

**Registration:** `services.AddWallowBackgroundJobs()` registers `HangfireJobScheduler` as the `IJobScheduler` implementation.

---

## Project Structure

The solution file is `api/Wallow.slnx`; everything below is relative to the repository root.

```
api/src/
  Wallow.Api/                        # Host -- wires all modules together
  Wallow.AppHost/                    # .NET Aspire host (orchestrates API, infra, React apps)
  Wallow.MigrationService/           # Applies module migrations (used outside Development)
  Wallow.SeederService/              # Seeds tenants, roles, and the bootstrap admin
  Wallow.ServiceDefaults/            # Shared Aspire service defaults (telemetry, health, resilience)
  Modules/
    Identity/
      Wallow.Identity.Domain/
      Wallow.Identity.Application/
      Wallow.Identity.Infrastructure/
      Wallow.Identity.Api/
    Announcements/                    # Same four-layer pattern
    ApiKeys/
    Branding/
    Inquiries/
    Notifications/
    Storage/
  Shared/
    Wallow.Shared.Contracts/                     # Cross-module events and DTOs
    Wallow.Shared.Kernel/                        # Base classes, multi-tenancy, shared abstractions
    Wallow.Shared.Api/                           # Shared API extensions, settings, health checks
    Wallow.Shared.Infrastructure/                # Settings and AsyncAPI document generation
    Wallow.Shared.Infrastructure.Core/           # Auditing, caching, messaging, persistence, middleware
    Wallow.Shared.Infrastructure.Plugins/        # Plugin loading and lifecycle
    Wallow.Shared.Infrastructure.BackgroundJobs/ # IJobScheduler / Hangfire


api/tests/
  Wallow.Api.Tests/
  Wallow.AppHost.Tests/
  Wallow.Architecture.Tests/
  Wallow.Shared.Kernel.Tests/
  Wallow.Shared.Infrastructure.Tests/
  Wallow.Tests.Common/               # Shared test utilities, fixtures, factories
  Benchmarks/                        # BenchmarkDotNet projects
  Modules/
    {Module}/
      Wallow.{Module}.Tests/         # One test project per module, with per-layer subfolders
```

Each module has a **single** `Wallow.{Module}.Tests` project rather than one project per layer; domain, application, and infrastructure tests live in subfolders inside it. Identity additionally has `Wallow.Identity.IntegrationTests` — the only project dedicated entirely to integration tests, but not the only place they live. Integration tests are selected by the `[Trait("Category", "Integration")]` marker, which appears across seven assemblies, so `./scripts/run-tests.sh integration` runs the whole solution rather than one project.

---

## Module Architecture

Every module follows Clean Architecture with four layers:

```
Domain         -> Entities, Value Objects, Domain Events. Zero dependencies.
Application    -> Commands, Queries, Handlers, DTOs, Interfaces. Depends on Domain.
Infrastructure -> EF Core, Consumers, external services. Implements Application interfaces.
Api            -> Controllers, request/response contracts. Depends on Application.
```

### Dependency Rules

- **Domain** references nothing.
- **Application** references Domain only.
- **Infrastructure** references Application and Domain.
- **Api** references Application only.
- Modules never reference each other directly. Cross-module communication goes through `Shared.Contracts` events.
- **Api** never references **Infrastructure** directly. It consumes Application interfaces.

---

## Adding a New Module

The step-by-step walkthrough lives in the
[Module Creation Guide](../architecture/module-creation.md) — it is the canonical reference for
project layout, layer contents, handler shapes, the DbContext/tenancy wiring, feature flags,
migrations, and tests. In outline: create the four class libraries under
`api/src/Modules/{Module}/` with the Clean Architecture references above, implement
`IWallowModule` once in the module's Infrastructure layer (naming its `HandlerAssemblies`,
`DbContextTypes`, and schema), and add one entry to `WallowModuleRegistry.All` in
`api/src/Wallow.Modules.Registry/WallowModuleRegistry.cs`.

That is almost the whole registration. `Wallow.Api` filters the registry against its
`FeatureManagement:Modules.*` configuration; `Wallow.MigrationService` takes it unfiltered. The
remaining touchpoints are a row in `_moduleApiAssemblies` in
`api/src/Wallow.Api/WallowModules.cs` naming one of the module's controller types (the host
refuses to start if that table and the registry disagree), an initial EF Core migration, and a
shorthand for the module in `scripts/run-tests.sh`'s `resolve_filter` case block.

Wolverine discovers handlers in exactly the assemblies each enabled module declares through
`IWallowModule.HandlerAssemblies` — no assembly scan, no per-handler registration — and routes
all module-to-module messages by type over the in-memory transport. A stateless module still
implements `IWallowModule`; it just returns an empty `DbContextTypes` list.

---

## Cross-Module Communication

Modules communicate through integration events published over Wolverine (in-memory bus). Events are defined in `Shared.Contracts`.

### Defining an Event

In `api/src/Shared/Wallow.Shared.Contracts/Inquiries/Events/`:

```csharp
public record SubmissionCreatedEvent(Guid SubmissionId, Guid UserId, DateTime OccurredAt);
```

Events are facts. Name them in past tense. They are not commands.

### Publishing

From any handler:

```csharp
await bus.PublishAsync(new SubmissionCreatedEvent(submission.Id, submission.UserId, DateTime.UtcNow));
```

### Consuming

In the consuming module's Infrastructure layer, create a handler. Wolverine discovers it by convention:

```csharp
public static class SubmissionCreatedEventHandler
{
    public static async Task HandleAsync(
        SubmissionCreatedEvent @event,
        IEmailService emailService,
        CancellationToken ct)
    {
        // React to the event
    }
}
```

---

## Commands and Queries

Wolverine acts as the CQRS mediator. No marker interfaces required.

### Command (Write)

`Application/Commands/CreateSubmission/CreateSubmissionCommand.cs`:
```csharp
public record CreateSubmissionCommand(Guid UserId, string Subject, string Body);
```

`Application/Commands/CreateSubmission/CreateSubmissionHandler.cs`:
```csharp
public static class CreateSubmissionHandler
{
    public static async Task<Result<SubmissionDto>> HandleAsync(
        CreateSubmissionCommand command,
        ISubmissionRepository repository,
        CancellationToken ct)
    {
        // ...
    }
}
```

### Query (Read)

Same pattern in `Application/Queries/`. For read-heavy queries, inject `IReadDbContext<{Module}DbContext>` and project straight to the DTO.

### Validation

FluentValidation validators are auto-discovered by Wolverine middleware:

```csharp
public class CreateSubmissionCommandValidator : AbstractValidator<CreateSubmissionCommand>
{
    public CreateSubmissionCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.Subject).NotEmpty();
    }
}
```

### Controller

```csharp
[HttpPost]
public async Task<IActionResult> CreateSubmission([FromBody] CreateSubmissionRequest request)
{
    var result = await _bus.InvokeAsync<Result<SubmissionDto>>(
        new CreateSubmissionCommand(request.UserId, request.Subject, request.Body));
    return result.ToActionResult();
}
```

---

## Multi-Tenancy

Tenant isolation is enforced at three layers.

### 1. Middleware

`TenantResolutionMiddleware` (in `Identity.Infrastructure/MultiTenancy/`) reads the `org_id` claim from the JWT and populates `ITenantContext` for the request scope.

### 2. Entity Marking

Domain entities that are tenant-scoped implement `ITenantScoped` from `Shared.Kernel`:

```csharp
public interface ITenantScoped
{
    TenantId TenantId { get; set; }
}
```

### 3. Query Filters

EF Core global query filters are applied by the `TenantAwareDbContext<T>` base class, not per entity. A module's `OnModelCreating` calls the inherited helper once:

```csharp
ApplyTenantQueryFilters(modelBuilder);
```

`HasQueryFilter` appears in exactly two files in `api/src` — `TenantAwareDbContext.cs` and Identity's `IdentityDbContext.cs` — and a module should never add a third. This scopes all queries to the current tenant automatically. To bypass (admin scenarios), use `.IgnoreQueryFilters()`.

### 4. Save Interceptor

`TenantSaveChangesInterceptor` automatically stamps `TenantId` on new entities and prevents modification of `TenantId` on updates.

### Raw SQL

Query filters only apply to LINQ over the DbContext. Any raw SQL you write must filter by tenant explicitly:

```sql
WHERE tenant_id = @TenantId
```

Pass `_tenantContext.TenantId.Value` as the parameter.

---

## Database

Each module owns its own schema. Migrations are per-module.

### Running Migrations

```bash
dotnet ef migrations add MigrationName \
    --project api/src/Modules/{Module}/Wallow.{Module}.Infrastructure \
    --startup-project api/src/Wallow.Api \
    --context {Module}DbContext

dotnet ef database update \
    --project api/src/Modules/{Module}/Wallow.{Module}.Infrastructure \
    --startup-project api/src/Wallow.Api \
    --context {Module}DbContext
```

> **Nothing migrates at application startup**, and modules have no startup hook to migrate from.
> `Wallow.MigrationService` applies migrations — as the `wallow-migrations` project resource
> under Aspire (`pnpm backend`), and as the `wallow-migrations` service in the Compose, E2E, staging
> and production stacks. The single exception is the `Testing` environment, where
> `WallowModules.RunTestMigrationsAsync` migrates inline for Testcontainers. See
> [Database Migrations](../development/database-migrations.md) for details.

### Write vs. Read Strategy

- **Writes**: EF Core through repositories.
- **Reads**: EF Core as well, through the `NoTracking` read context registered by `AddReadDbContext<T>()`, which routes to the read replica when `ReadReplicaConnection` is configured.

---

## Naming Conventions

| Item | Convention | Example |
|------|------------|---------|
| Projects | `Wallow.{Module}.{Layer}` | `Wallow.Inquiries.Domain` |
| Commands | Verb + Noun + Command | `CreateSubmissionCommand` |
| Queries | Get + Noun + Query | `GetSubmissionByIdQuery` |
| Handlers | Command/Query name + Handler | `CreateSubmissionHandler` |
| Events | Noun + PastTense + Event | `SubmissionCreatedEvent` |
| DTOs | Noun + Dto | `SubmissionDto` |
| Requests | Verb + Noun + Request | `CreateSubmissionRequest` |
| Responses | Noun + Response | `SubmissionResponse` |
| DB Schemas | snake_case module name | `inquiries`, `identity` |

One class per file. File name matches class name. Commands and queries get their own folder:

```
Commands/
  CreateSubmission/
    CreateSubmissionCommand.cs
    CreateSubmissionHandler.cs
```

---

## Request Flow

```
HTTP Request
  -> Controller (Api)
  -> Command/Query (Application)
  -> Wolverine IMessageBus
  -> Handler (Application)
  -> Repository (Infrastructure, via interface)
  -> DTO (Application)
  -> Response (Api)
  -> HTTP Response
```

Validation runs automatically before handlers via Wolverine's FluentValidation middleware.

---

## Testing

### Test Infrastructure

Wallow uses **xUnit** as the test framework, **AwesomeAssertions** for readable assertions, and **Testcontainers** for integration tests that need real infrastructure (PostgreSQL, Valkey).

Shared test utilities live in `api/tests/Wallow.Tests.Common/`, including:
- `WallowApiFactory` -- `WebApplicationFactory` configured with Testcontainers
- `DatabaseFixture`, `RedisFixture` -- reusable xUnit fixtures
- `Builders/`, `Fakes/`, `Helpers/` -- test data builders and utilities

### Unit Tests

Test handlers in isolation. Mock repositories and services:

```csharp
[Fact]
public async Task Should_create_submission()
{
    // Arrange
    var repo = Substitute.For<ISubmissionRepository>();
    var command = new CreateSubmissionCommand(userId, "Help needed", "I cannot log in.");

    // Act
    var result = await CreateSubmissionHandler.HandleAsync(command, repo, CancellationToken.None);

    // Assert
    result.IsSuccess.Should().BeTrue();
    await repo.Received(1).SaveChangesAsync();
}
```

### Integration Tests

Use `WallowApiFactory` with Testcontainers:

```csharp
public class InquiriesControllerTests : IClassFixture<WallowApiFactory>
{
    private readonly HttpClient _client;

    public InquiriesControllerTests(WallowApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Submit_returns_200()
    {
        HttpResponseMessage response = await _client.PostAsJsonAsync("/v1/inquiries", request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
```

### Architecture Tests

`Wallow.Architecture.Tests` validates structural rules (e.g., modules do not reference each other, dependency direction is correct). These run as part of the standard test suite.

---

## Technology Stack

| Purpose | Technology |
|---------|------------|
| Framework | .NET 10 |
| Database | PostgreSQL 18 |
| ORM | EF Core |
| CQRS & Messaging | Wolverine (mediator + in-memory bus) |
| Logging | Serilog |
| Real-time | SignalR |
| Validation | FluentValidation |
| Identity / Auth | ASP.NET Core Identity + OpenIddict |
| Caching | Valkey (Redis-compatible) |
| Object Storage | GarageHQ (S3-compatible) |
| Testing | xUnit, Testcontainers, AwesomeAssertions |

---

## Troubleshooting

**DB connection failures**: Verify Postgres is running with `docker compose ps`. Check connection strings in `appsettings.Development.json`.

**GarageHQ issues**: Check admin API at http://localhost:3903. Verify the bucket exists with `docker exec wallow-garage garage bucket list`. If the init script failed, restart with `docker compose restart garage`.

**Tests failing**: Integration tests need Docker. Run `docker ps` to verify. Testcontainers creates ephemeral containers; ensure Docker has enough resources.

**Reset everything**: `cd docker && docker compose down -v && cd .. && pnpm backend`. The `-v` is what drops the volumes (`pnpm backend:infra:down` keeps them), and `pnpm backend` brings the stack back up through Aspire so `wallow-migrations` and the seeder run before the API does.
