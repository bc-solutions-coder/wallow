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

The workspace's quality gate is `pnpm check`: format check, both lint passes, manifest/dependency/env checks, then `turbo run build typecheck test` and `check:exports`. It is what `js.yml` runs in CI, so run it before opening a PR that touches `apps/` or `packages/`. Turbo caches build, typecheck, test and dev locally in `.turbo/`, so a warm run is far faster than the first.

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

## Issue Tracking with Beads

Wallow tracks work with **bd** (beads), a lightweight issue tracker whose database is embedded directly in the repository rather than hosted separately:

```bash
bd ready                                    # Find available work
bd show <id>                                # View issue details
bd update <id> --status in_progress         # Claim work
bd note <id> "..."                          # Record a finding on the issue
bd close <id>                               # Complete work
```

This page is the canonical bd workflow for Wallow. `.beads/README.md` is **not** -- it is stock
`bd init` boilerplate, it is untracked (see below), and `bd init` regenerates it on every fresh
setup, so nothing written there survives to another clone. Read it as vendor documentation for the
tool, not as instructions for this repository.

### Where the data lives

Beads live in an embedded [Dolt](https://www.dolthub.com/) database under `.beads/embeddeddolt/<db>/`, where `<db>` is the name of the Dolt database itself. That name is **per-machine, not repo-wide**: a machine where `bd init` created the database gets `Wallow`, while a machine set up with `bd bootstrap` (see below) gets `beads`. Nothing in the tooling depends on this name, so never hardcode the path in a script or doc.

All of `.beads/` is gitignored -- none of it is committed to the repository. Instead, beads data travels through the **same GitHub repository** using Dolt's own git-backed remote: issue history sits on `refs/dolt/data`, with a `__dolt_remote_info__` branch pointing at it. `.beads/config.yaml` records `sync.remote: git+https://github.com/bc-solutions-coder/wallow.git`, so beads sync over **HTTPS**, using whatever git credential helper already authenticates `git push` for this repo — not a `.npmrc` credential.

### Pushing and pulling

Because beads data does not live in the normal git history, ordinary `git push` and `git pull` do not move it. You must sync it explicitly:

- **`bd dolt push`** -- pushes local issue changes to `refs/dolt/data` on the remote. You must run this **explicitly**; a plain `git push` does *not* carry beads along, because the `pre-push` hook exits 0 without touching `refs/dolt/data`. Skip this step and any issues you created or updated stay stranded on your machine.
- **`bd dolt pull`** -- pulls issue changes from the remote before you start work on a machine you have not used in a while. Like the push side, `git pull` does **not** bring beads over.

### Setting up a new machine

On a fresh clone, run:

```bash
bd bootstrap --yes
```

This finds `refs/dolt/data` on the git origin and rebuilds the whole beads database from it -- no `.beads/` directory needs to exist beforehand -- and wires up `origin` as the sync remote for later `bd dolt push`/`bd dolt pull`, so there is no manual `bd dolt remote add` step.

### Do not run `bd hooks install`

Wallow's git hooks are owned by **husky**, not by beads. Every `pnpm install` runs the `prepare: husky` script, which sets `core.hooksPath=.husky/_`, and the tracked `.husky/*` hook files already contain the bridge block that keeps bd in sync on commit/push. This means beads integration works out of the box with no extra setup (`bd hooks list` will confirm it).

Running `bd hooks install` fights husky for the same git configuration: it repoints `core.hooksPath` at `.beads/hooks`, copies the husky hook bodies into that new location, and appends a second bridge block on top. The result is that every hook runs its beads logic twice, edits to `.husky/` silently stop taking effect because git is no longer looking there, and the next `pnpm install` flips `core.hooksPath` back to husky's directory -- leaving the repo in an inconsistent state either way.

If hooks ever look wrong, recover with:

```bash
bd hooks uninstall   # clears core.hooksPath entirely, leaving no hooks configured
pnpm exec husky       # re-installs husky's hooks and restores core.hooksPath=.husky/_
```

### Diagnostics

`bd dolt remote list` prints the configured remote and is useful for confirming sync is wired up correctly. Note that the standalone `dolt` CLI is not installed in this environment -- `bd` embeds the Dolt engine itself -- so raw `dolt` commands are unavailable, and `bd doctor` will report that diagnostics are "not yet supported in embedded mode".

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

An EF Core `SaveChangesInterceptor` that automatically captures all entity changes (inserts, updates, deletes) across every module's DbContext. Audit entries include the entity type, primary key, old/new values (serialized JSON), the acting user, tenant, and timestamp. Entries are stored in a dedicated `audit` schema via `AuditDbContext`.

**Registration:** `services.AddWallowAuditing(configuration)` registers the `AuditDbContext` and `AuditInterceptor` singleton. Module DbContexts pick up auditing automatically by adding the interceptor to their options (via `options.AddInterceptors(sp.GetRequiredService<AuditInterceptor>())`).

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

This guide walks through creating a new standard (EF Core) module using extension methods.

### Step 1: Create the Projects

Under `api/src/Modules/{Module}/`, create four class libraries. Example for a "Tickets" module:

```bash
dotnet new classlib -o api/src/Modules/Tickets/Wallow.Tickets.Domain
dotnet new classlib -o api/src/Modules/Tickets/Wallow.Tickets.Application
dotnet new classlib -o api/src/Modules/Tickets/Wallow.Tickets.Infrastructure
dotnet new classlib -o api/src/Modules/Tickets/Wallow.Tickets.Api
```

Set project references:
- `Application` -> `Domain`
- `Infrastructure` -> `Application`, `Shared.Kernel`, `Shared.Contracts`
- `Api` -> `Application`, `Shared.Kernel`

### Step 2: Define Domain Entities

Place entities in `Wallow.{Module}.Domain/Entities/`. Inherit from `Entity` or `AggregateRoot`:

```csharp
public class Ticket : AggregateRoot, ITenantScoped
{
    public string Title { get; private set; }
    public TicketStatus Status { get; private set; }
    public TenantId TenantId { get; set; }
}
```

### Step 3: Create Application Services

Define commands, queries, and handlers:

```csharp
// Commands/CreateTicket/CreateTicketCommand.cs
public record CreateTicketCommand(string Title, string Description);

// Commands/CreateTicket/CreateTicketHandler.cs
public static class CreateTicketHandler
{
    public static async Task<Result<TicketDto>> HandleAsync(
        CreateTicketCommand command,
        ITicketRepository repository,
        CancellationToken ct)
    {
        // Implementation
    }
}
```

Create the Application extension:

```csharp
// Extensions/ApplicationExtensions.cs
public static class ApplicationExtensions
{
    public static IServiceCollection AddTicketsApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(ApplicationExtensions).Assembly);
        return services;
    }
}
```

### Step 4: Create Infrastructure Layer

Create the DbContext. Derive from `TenantAwareDbContext<T>` (in `Wallow.Shared.Infrastructure.Core.Persistence`) and call `ApplyTenantQueryFilters(modelBuilder)` — **never hand-roll a `HasQueryFilter` per entity**. The base class applies the tenant filter to every entity implementing the tenant marker interface, so a new entity is scoped the moment it is added:

```csharp
// Persistence/TicketsDbContext.cs
public sealed class TicketsDbContext : TenantAwareDbContext<TicketsDbContext>
{
    public DbSet<Ticket> Tickets => Set<Ticket>();

    public TicketsDbContext(DbContextOptions<TicketsDbContext> options)
        : base(options)
    {
        ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("tickets");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TicketsDbContext).Assembly);

        ApplyTenantQueryFilters(modelBuilder);
    }
}
```

Create the Infrastructure extension. Modules register a **pooled DbContext factory** plus `AddTenantAwareScopedContext<T>()`, which is what resolves a tenant-scoped context per request:

```csharp
// Extensions/InfrastructureExtensions.cs
public static class InfrastructureExtensions
{
    public static IServiceCollection AddTicketsInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        int maxPoolSize = configuration.GetValue("Database:MaxPoolSize", 200);
        int minPoolSize = configuration.GetValue("Database:MinPoolSize", 10);

        string defaultConnectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");

        services.AddPooledDbContextFactory<TicketsDbContext>((sp, options) =>
        {
            NpgsqlConnectionStringBuilder builder = new(defaultConnectionString)
            {
                MaxPoolSize = maxPoolSize,
                MinPoolSize = minPoolSize
            };
            options.UseNpgsql(builder.ConnectionString, npgsql =>
            {
                // Each module gets its own migration history table in its own schema
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "tickets");
                npgsql.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(30),
                    errorCodesToAdd: null);
                npgsql.CommandTimeout(30);
            });
            options.AddInterceptors(sp.GetRequiredService<TenantSaveChangesInterceptor>());
        });

        services.AddTenantAwareScopedContext<TicketsDbContext>();
        services.AddReadDbContext<TicketsDbContext>(configuration);

        services.AddScoped<ITicketRepository, TicketRepository>();
        return services;
    }
}
```

`AnnouncementsModuleExtensions.cs` is the reference implementation of this shape; [Database Migrations](../development/database-migrations.md) documents the same registration in more detail.

### Step 5: Create Module Extension Methods

Create the module extension methods in Infrastructure:

```csharp
// api/src/Modules/Tickets/Wallow.Tickets.Infrastructure/Extensions/TicketsModuleExtensions.cs
using Wallow.Tickets.Application.Extensions;
using Wallow.Tickets.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Wallow.Tickets.Infrastructure.Extensions;

public static class TicketsModuleExtensions
{
    public static IServiceCollection AddTicketsModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddTicketsApplication();
        services.AddTicketsInfrastructure(configuration);
        return services;
    }
}
```

> **Modules do not migrate themselves, and they have no startup hook.** Migrations run through
> `Wallow.MigrationService` — under Aspire as the `wallow-migrations` project resource, in Compose as
> the `wallow-migrations` service. The one exception is the `Testing` environment, where
> `WallowModules.RunTestMigrationsAsync` migrates inline; it takes the enabled module set and
> migrates each module's `DbContextTypes`, so a new module needs no registration there. See
> [Database Migrations](../development/database-migrations.md).

### Step 6: Implement IWallowModule and Add It to the Registry

Describe the module once, in its Infrastructure layer:

```csharp
// api/src/Modules/Tickets/Wallow.Tickets.Infrastructure/Modules/TicketsModule.cs
public sealed class TicketsModule : IWallowModule
{
    internal const string Schema = "tickets";

    public string Name => "Tickets";

    public bool IsCore => false;

    public IReadOnlyList<Assembly> HandlerAssemblies =>
    [
        typeof(CreateTicketHandler).Assembly,  // .Application
        typeof(TicketsModule).Assembly,        // .Infrastructure
    ];

    public IReadOnlyList<Type> DbContextTypes => [typeof(TicketsDbContext)];

    public string SchemaName => Schema;

    public IServiceCollection AddServices(
        IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment) =>
        services.AddTicketsModule(configuration);
}
```

Then add one entry to `WallowModuleRegistry.All` in
`api/src/Wallow.Modules.Registry/WallowModuleRegistry.cs`:

```csharp
new TicketsModule(),
```

That is the whole registration. `Wallow.Api` filters the registry against its
`FeatureManagement:Modules.*` configuration; `Wallow.MigrationService` takes it unfiltered. The one
remaining host-side edit is a row in `_moduleApiAssemblies` in `api/src/Wallow.Api/WallowModules.cs`
naming one of the module's controller types, so a disabled module's routes can be removed — the host
refuses to start if that table and the registry disagree.

### Step 7: Create Initial Migration

```bash
dotnet ef migrations add InitialCreate \
    --project api/src/Modules/Tickets/Wallow.Tickets.Infrastructure \
    --startup-project api/src/Wallow.Api \
    --context TicketsDbContext
```

### Step 8: Add Tests

Create a single test project, `api/tests/Modules/Tickets/Wallow.Tickets.Tests/`, and organize it with one subfolder per layer:
- `Domain/` -- Unit tests for domain entities and value objects
- `Application/` -- Unit tests for handlers and validators
- `Infrastructure/` -- Unit tests for repositories (optional)

Then add a shorthand for the new module to the `resolve_filter` case block in `scripts/run-tests.sh` so `./scripts/run-tests.sh tickets` works.

### Handler Discovery

Wolverine discovers handlers in exactly the assemblies each enabled module declares through
`IWallowModule.HandlerAssemblies` — there is no assembly scan. Because that list names both the
module's `.Application` and its `.Infrastructure` assembly, a handler added to either one needs no
registration of its own. Just create handlers following Wolverine conventions:

```csharp
public static class CreateSubmissionHandler
{
    public static async Task<Result<SubmissionDto>> HandleAsync(
        CreateSubmissionCommand command,
        ISubmissionRepository repository,
        CancellationToken ct)
    {
        // Implementation
    }
}
```

### Message Routing

Wolverine uses in-memory transport for all module-to-module messaging. Messages are routed automatically by type -- no manual routing configuration is needed.

### Module Type Examples

#### Standard Module (with EF Core persistence)
See the Inquiries module: `api/src/Modules/Inquiries/Wallow.Inquiries.Infrastructure/Extensions/InquiriesModuleExtensions.cs`

#### Stateless Module (no persistence)
```csharp
public static class ExampleModuleExtensions
{
    public static IServiceCollection AddExampleModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddExampleInfrastructure(configuration);
        return services;
    }
}
```

A stateless module still implements `IWallowModule`; it just returns an empty
`DbContextTypes` list.

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
