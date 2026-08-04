# Module Creation Guide

Step-by-step guide for adding a new module to Wallow.

---

## Prerequisites

Before creating a new module:

- Understand Clean Architecture layers (Domain, Application, Infrastructure, Api)
- Review the Inquiries module (`api/src/Modules/Inquiries/`) as a reference implementation — but read
  the handler-shape rule in [Step 4](#step-4-create-application-layer) first: Inquiries' command
  handlers are static, which is the older of the two shapes in the tree
- Decide on your module name (PascalCase, singular noun)
- Identify primary entities and their relationships
- Determine if the module needs database persistence (EF Core) or is stateless

> **Current modules:** Identity, Storage, Notifications, Announcements, Inquiries, ApiKeys, Branding. New modules should complement these existing capabilities.

---

## Quick Start Checklist

| Step | Action |
|------|--------|
| 1 | Create 4 class library projects |
| 2 | Configure project references |
| 3 | Create Domain layer (IDs, entities, events) |
| 4 | Create Application layer (commands, queries, handlers, interfaces) |
| 5 | Create Infrastructure layer (DbContext, repositories, DI extensions) |
| 6 | Create API layer (controllers, request/response contracts) |
| 7 | Register in `WallowModules.cs` |
| 8 | Add feature flag to `appsettings.json` |
| 9 | Create database migration |
| 10 | Add tests |
| 11 | Define integration events in `Shared.Contracts` (if needed) |

---

## Step 1: Create Project Structure

Create 4 class library projects following the naming convention `Wallow.{Module}.{Layer}`:

```bash
mkdir -p api/src/Modules/{Module}

dotnet new classlib -n Wallow.{Module}.Domain -o api/src/Modules/{Module}/Wallow.{Module}.Domain
dotnet new classlib -n Wallow.{Module}.Application -o api/src/Modules/{Module}/Wallow.{Module}.Application
dotnet new classlib -n Wallow.{Module}.Infrastructure -o api/src/Modules/{Module}/Wallow.{Module}.Infrastructure
dotnet new classlib -n Wallow.{Module}.Api -o api/src/Modules/{Module}/Wallow.{Module}.Api

dotnet sln api/Wallow.slnx add api/src/Modules/{Module}/**/*.csproj
```

There is no solution file at the repository root — `api/Wallow.slnx` is the only one, so the path is
required. `**` also needs recursive globbing enabled in your shell (`shopt -s globstar` in bash; zsh
expands it by default). If the glob does not expand, add the four `.csproj` files individually.

**Directory structure:**

```
api/src/Modules/{Module}/
├── Wallow.{Module}.Domain/
│   ├── Identity/              # Strongly-typed IDs
│   ├── Entities/              # Domain entities and aggregate roots
│   ├── Enums/                 # Enumerations
│   ├── Events/                # Domain events
│   ├── ValueObjects/          # Value objects (optional)
│   └── Exceptions/            # Custom exceptions (optional)
│
├── Wallow.{Module}.Application/
│   ├── Commands/              # CQRS command handlers
│   ├── Queries/               # CQRS query handlers
│   ├── DTOs/                  # Data transfer objects
│   ├── Interfaces/            # Repository contracts
│   ├── EventHandlers/         # Domain-to-integration event bridges
│   ├── Extensions/            # Application layer DI registration
│   ├── Mappings/              # Entity-to-DTO mappings (optional)
│   └── Validators/            # FluentValidation validators (optional)
│
├── Wallow.{Module}.Infrastructure/
│   ├── Extensions/            # DI registration and module extensions
│   ├── Persistence/           # DbContext, repositories
│   │   ├── Configurations/    # EF Core entity configurations
│   │   └── Repositories/      # Repository implementations
│   └── Migrations/            # EF Core migrations
│
└── Wallow.{Module}.Api/
    ├── Controllers/           # API endpoints
    └── Contracts/             # Request/Response DTOs
```

---

## Step 2: Configure Project References

Each layer has strict dependency rules.

**Domain** references only `Shared.Kernel` (no other dependencies):

```xml
<ItemGroup>
  <ProjectReference Include="..\..\..\Shared\Wallow.Shared.Kernel\Wallow.Shared.Kernel.csproj" />
</ItemGroup>
```

**Application** references Domain, `Shared.Kernel`, and `Shared.Contracts`:

```xml
<ItemGroup>
  <ProjectReference Include="..\Wallow.{Module}.Domain\Wallow.{Module}.Domain.csproj" />
  <ProjectReference Include="..\..\..\Shared\Wallow.Shared.Kernel\Wallow.Shared.Kernel.csproj" />
  <ProjectReference Include="..\..\..\Shared\Wallow.Shared.Contracts\Wallow.Shared.Contracts.csproj" />
</ItemGroup>
<ItemGroup>
  <PackageReference Include="FluentValidation" />
  <PackageReference Include="FluentValidation.DependencyInjectionExtensions" />
  <PackageReference Include="WolverineFx" />
</ItemGroup>
```

`WolverineFx` is required here, not optional: the command handler in Step 4 and the domain-event
bridge later in that step both inject `IMessageBus`, and `Wallow.Shared.Contracts` carries no package
references of its own, so Wolverine cannot arrive transitively.

**Infrastructure** references Domain, Application, and `Shared.Infrastructure`:

```xml
<ItemGroup>
  <PackageReference Include="Microsoft.EntityFrameworkCore" />
  <PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" />
  <PackageReference Include="Microsoft.EntityFrameworkCore.Design">
    <PrivateAssets>all</PrivateAssets>
    <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
  </PackageReference>
  <!-- Add only what the module actually uses: -->
  <PackageReference Include="Dapper" />               <!-- complex read queries -->
  <PackageReference Include="StackExchange.Redis" />  <!-- direct Valkey access -->
  <PackageReference Include="WolverineFx" />          <!-- publishing integration events from Infrastructure -->
</ItemGroup>
<ItemGroup>
  <ProjectReference Include="..\Wallow.{Module}.Domain\Wallow.{Module}.Domain.csproj" />
  <ProjectReference Include="..\Wallow.{Module}.Application\Wallow.{Module}.Application.csproj" />
  <ProjectReference Include="..\..\..\Shared\Wallow.Shared.Infrastructure\Wallow.Shared.Infrastructure.csproj" />
</ItemGroup>
```

The last three are genuinely optional, unlike the Application layer's `WolverineFx`. `Wallow.Storage.Infrastructure`
carries none of them and `Wallow.Identity.Infrastructure` carries only `StackExchange.Redis`.

**Api** references Application and `Shared.Api` — never its own Infrastructure:

```xml
<ItemGroup>
  <FrameworkReference Include="Microsoft.AspNetCore.App" />
</ItemGroup>
<ItemGroup>
  <ProjectReference Include="..\Wallow.{Module}.Application\Wallow.{Module}.Application.csproj" />
  <ProjectReference Include="..\..\..\Shared\Wallow.Shared.Api\Wallow.Shared.Api.csproj" />
</ItemGroup>
```

`Wallow.Shared.Api` is required: `ToActionResult()`, which Step 6 uses on every controller action, is
defined there and nowhere else.

Module registration is handled by Infrastructure extensions called from `WallowModules.cs`, so the Api layer never needs to reference Infrastructure directly.

> **Exception in the tree:** `Wallow.Identity.Api` does reference `Wallow.Identity.Infrastructure`,
> because its controllers reach ASP.NET Core Identity services that Infrastructure hosts. It is the
> only module that does, and new modules should not copy it. Some module Api projects also take
> additional *shared* projects — Announcements takes `Shared.Infrastructure`, ApiKeys takes
> `Shared.Contracts` and `Shared.Kernel` — which the rule above does not forbid.

---

## Step 3: Create Domain Layer

### Strongly-Typed ID

Every entity needs a strongly-typed ID that implements `IStronglyTypedId<T>`:

```csharp
// Identity/{Entity}Id.cs
using Wallow.Shared.Kernel.Identity;

namespace Wallow.{Module}.Domain.Identity;

public readonly record struct {Entity}Id(Guid Value) : IStronglyTypedId<{Entity}Id>
{
    public static {Entity}Id Create(Guid value) => new(value);
    public static {Entity}Id New() => new(Guid.NewGuid());
}
```

### Domain Entity

Aggregate roots extend `AggregateRoot<TId>` and implement `ITenantScoped` for multi-tenancy. Use factory methods instead of public constructors. Raise domain events from entity methods.

Key conventions:
- Private parameterless constructor for EF Core
- `Create()` static factory method that validates input and raises a domain event
- Call `SetCreated(userId)` and `SetUpdated(userId)` for audit fields
- Throw `BusinessRuleException` for domain rule violations

### Domain Event

Domain events are simple records extending `DomainEvent`:

```csharp
// Events/{Entity}CreatedDomainEvent.cs
using Wallow.Shared.Kernel.Domain;

namespace Wallow.{Module}.Domain.Events;

public sealed record {Entity}CreatedDomainEvent(Guid {Entity}Id, string Name) : DomainEvent;
```

---

## Step 4: Create Application Layer

### Handler shape — the folder decides

Wolverine accepts two handler shapes and the tree uses both. The boundary is the **folder**, not the
message kind:

| Folder | Shape | Dependencies arrive via |
|--------|-------|------------------------|
| `Commands/`, `Queries/` | `public sealed class …Handler(…)` — instance class, primary constructor | constructor parameters |
| `EventHandlers/` (and Identity's `Handlers/`) | `public static class …Handler` | `Handle`/`HandleAsync` method parameters |

Use the primary-constructor shape for new `Commands/` and `Queries/` handlers. Four command handlers
depart from it and are static: Inquiries' three (`SubmitInquiryHandler`, `AddInquiryCommentHandler`,
`UpdateInquiryStatusHandler`) and Storage's `ScanUploadedFileHandler`, which is a
`public static partial class`. Copy Inquiries' *structure* but not its command-handler shape. Its
four `Queries/` handlers are instance classes and do follow the rule.

### Command and Handler

Commands are plain records. Handlers use the primary constructor pattern with Wolverine:

```csharp
// Commands/Create{Entity}/Create{Entity}Command.cs
public sealed record Create{Entity}Command(string Name);
```

```csharp
// Commands/Create{Entity}/Create{Entity}Handler.cs
public sealed class Create{Entity}Handler(
    I{Entity}Repository repository,
    IMessageBus messageBus)
{
    public async Task<Result<{Entity}Dto>> Handle(
        Create{Entity}Command command,
        CancellationToken cancellationToken)
    {
        // Validate, create entity, persist, return DTO
    }
}
```

Wolverine auto-discovers handlers in all `Wallow.*` assemblies. No manual registration is needed.

### Query and Handler

Use the same pattern for queries. For complex read queries, define an `I{Module}QueryService` interface and implement it with Dapper in the Infrastructure layer. Use EF Core repositories for simple lookups.

### Repository Interface

Define repository contracts in the Application layer:

```csharp
// Interfaces/I{Entity}Repository.cs
public interface I{Entity}Repository
{
    Task<{Entity}?> GetByIdAsync({Entity}Id id, CancellationToken cancellationToken = default);
    void Add({Entity} entity);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
```

### DTOs and Mappings

DTOs are sealed records in the `DTOs/` folder. Mapping extension methods live in `Mappings/{Entity}Mappings.cs` and convert entities to DTOs via `ToDto()` extension methods.

### Validators

Use FluentValidation. Validators are auto-registered via `AddValidatorsFromAssembly` in the Application extensions.

### Domain Event Handler (Bridge to Integration Events)

Domain event handlers in the Application layer translate domain events into integration events for cross-module consumption:

```csharp
public static class {Entity}CreatedDomainEventHandler
{
    public static async Task HandleAsync(
        {Entity}CreatedDomainEvent domainEvent,
        IMessageBus bus,
        CancellationToken cancellationToken)
    {
        await bus.PublishAsync(new {Entity}CreatedEvent
        {
            {Entity}Id = domainEvent.{Entity}Id,
            Name = domainEvent.Name,
        });
    }
}
```

### Application Extensions

Register validators in an extension method:

```csharp
// Extensions/ApplicationExtensions.cs
public static class ApplicationExtensions
{
    public static IServiceCollection Add{Module}Application(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(ApplicationExtensions).Assembly);
        return services;
    }
}
```

---

## Step 5: Create Infrastructure Layer

### DbContext

Each module owns its own PostgreSQL schema (lowercase module name). Extend
`TenantAwareDbContext<TContext>` — do **not** hand-roll the multi-tenancy plumbing:

```csharp
public sealed class {Module}DbContext : TenantAwareDbContext<{Module}DbContext>
{
    public {Module}DbContext(DbContextOptions<{Module}DbContext> options)
        : base(options)
    {
        ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("{modulename}");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof({Module}DbContext).Assembly);

        ApplyTenantQueryFilters(modelBuilder);
    }
}
```

The base class (`api/src/Shared/Wallow.Shared.Infrastructure.Core/Persistence/TenantAwareDbContext.cs`)
owns the multi-tenancy machinery: `ApplyTenantQueryFilters` walks the model and adds the
`WHERE tenant_id = …` filter to every `ITenantScoped` entity via expression trees, and the context
tracks the current tenant through `SetTenant`/`CurrentTenantId`. You call it; you do not reimplement
it. `NoTracking` is the repo-wide default — mutations attach explicitly.

All six non-Identity modules extend this base class. Identity is the exception only because it must
extend `AspNetIdentityDbContext`, so it implements `ITenantAwareContext` directly instead.

See `InquiriesDbContext` for the reference implementation this snippet is drawn from.

### Entity Configuration

EF Core entity configurations follow these conventions:
- Table names: lowercase, plural (e.g., `invoices`)
- Column names: snake_case (e.g., `created_at`, `tenant_id`)
- Primary key: use `StronglyTypedIdConverter<TId>()` with `ValueGeneratedNever()`
- TenantId: convert with `id => id.Value, value => TenantId.Create(value)`
- Always add an index on `tenant_id`
- Always call `builder.Ignore(e => e.DomainEvents)`

### Repository Implementation

Repositories implement the Application layer interfaces using the module's DbContext.

### Design-Time Factory

Required for `dotnet ef migrations` to work. Create `{Module}DbContextFactory` implementing `IDesignTimeDbContextFactory<{Module}DbContext>` with a placeholder connection string and a `DesignTimeTenantContext` mock. See `InquiriesDbContextFactory` for reference.

### Infrastructure Extensions

Two extension classes in the Infrastructure layer:

**`{Module}InfrastructureExtensions.cs`** registers the DbContext (with `TenantSaveChangesInterceptor`), repositories, and any module-specific services. Pool sizes and the connection string come from configuration (`Database:MaxPoolSize`, `Database:MinPoolSize`, `ConnectionStrings:DefaultConnection`):

```csharp
services.AddPooledDbContextFactory<{Module}DbContext>((sp, options) =>
{
    NpgsqlConnectionStringBuilder builder = new(defaultConnectionString)
    {
        MaxPoolSize = maxPoolSize,
        MinPoolSize = minPoolSize
    };
    options.UseNpgsql(builder.ConnectionString, npgsql =>
    {
        // Each module keeps its migration history table in its own schema
        npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "{modulename}");
        npgsql.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(30),
            errorCodesToAdd: null);
        npgsql.CommandTimeout(30);
    });
    options.AddInterceptors(sp.GetRequiredService<TenantSaveChangesInterceptor>());
});

services.AddTenantAwareScopedContext<{Module}DbContext>();
services.AddReadDbContext<{Module}DbContext>(configuration);
```

**Never register the context with `AddDbContext`.** The registration is deliberately split in two:
the pooled factory owns the options, and `AddTenantAwareScopedContext<T>`
(`api/src/Shared/Wallow.Shared.Infrastructure.Core/Extensions/TenantAwareDbContextExtensions.cs`)
adds the scoped registration that pulls an instance out of that factory and calls `SetTenant` on it
with the current `ITenantContext` tenant, falling back to `AmbientTenant.Current` for scopes that
have not resolved one yet (Wolverine handlers). That call is the **only** place `SetTenant` is
invoked. A context resolved from a plain `AddDbContext` registration is therefore never primed, so
`ApplyTenantQueryFilters` runs against an unset tenant and the isolation it looks like it provides
is not there. `AddReadDbContext<T>` registers the Dapper read connection used for complex reads.

Identity is the exception again: because `IdentityDbContext` implements `ITenantAwareContext`
directly rather than extending `TenantAwareDbContext<T>`, it cannot use the generic helper and
inlines the same factory-plus-`SetTenant` scoped registration by hand.

**`{Module}ModuleExtensions.cs`** provides the two entry points called by `WallowModules.cs`:

```csharp
public static IServiceCollection Add{Module}Module(
    this IServiceCollection services, IConfiguration configuration)
{
    services.Add{Module}Application();
    services.Add{Module}Infrastructure(configuration);
    return services;
}

public static Task<WebApplication> Initialize{Module}ModuleAsync(this WebApplication app)
{
    return Task.FromResult(app);
}
```

**Do not migrate here.** All seven existing modules' `Initialize{Module}ModuleAsync` methods are
`return Task.FromResult(app);`. Migrations run through `Wallow.MigrationService`, a separate project
the Aspire AppHost runs before the API starts. The only in-process migration path is
`RunTestMigrationsAsync` in `WallowModules.cs`, and it runs only when the environment is `Testing`.
The hook exists for module initialization work that genuinely must happen at startup — seeding a
cache, registering a plugin — not for schema.

---

## Step 6: Create API Layer

### Controller

Controllers use Wolverine's `IMessageBus` to dispatch commands and queries. Follow these conventions:
- Route: `[Route("v{version:apiVersion}/{modulename}/{entities}")]` (all lowercase). API versioning is
  applied through the URL segment, so the live path is `/v1/{modulename}/{entities}`. Do **not** add
  an `api/` prefix — no controller in the repo has one, and a documented `/api/…` path 404s. An
  `/api` prefix appears only when a reverse proxy adds it via the opt-in PathBase
  (`api/src/Wallow.Api/Program.cs`).
- Annotate with `[Authorize]`, `[Tags("{Entities}")]`, `[Produces("application/json")]`
- Map Application DTOs to API-layer response records in the controller
- Use `Result<T>` from `Wallow.Shared.Kernel.Results` and convert with `.ToActionResult()`

### Request/Response Contracts

Define sealed records in the `Contracts/` folder. Keep them separate from Application DTOs so the API contract can evolve independently.

---

## Step 7: Register in WallowModules.cs

Edit `api/src/Wallow.Api/WallowModules.cs`:

1. Add the using statement: `using Wallow.{Module}.Infrastructure.Extensions;`

2. In `AddWallowModules()`, add the feature-flagged registration in the appropriate section (platform or feature modules):

```csharp
if (featureManager.IsEnabledAsync("Modules.{Module}").GetAwaiter().GetResult())
{
    services.Add{Module}Module(configuration);
}
```

3. In `InitializeWallowModulesAsync()`, add the corresponding initialization:

```csharp
if (await featureManager.IsEnabledAsync("Modules.{Module}"))
{
    await app.Initialize{Module}ModuleAsync();
}
```

> **Note:** Identity is a required platform dependency and is always registered without a feature flag. All other modules are behind feature flags.

---

## Step 8: Add Feature Flag

Add the feature flag to `appsettings.json` under `FeatureManagement`:

```json
{
  "FeatureManagement": {
    "Modules.{Module}": true
  }
}
```

---

## Step 9: Create Database Migration

```bash
dotnet ef migrations add InitialCreate \
    --project api/src/Modules/{Module}/Wallow.{Module}.Infrastructure \
    --startup-project api/src/Wallow.Api \
    --context {Module}DbContext
```

Nothing migrates the new schema on API startup. Two registrations are required, and both are easy to
miss:

1. **`Wallow.MigrationService`** applies migrations for every environment except `Testing`. Register
   the new `DbContext` there or the schema is never created.
2. **`RunTestMigrationsAsync`** in `api/src/Wallow.Api/WallowModules.cs` is the `Testing`-only
   in-process path. Add a `MigrateIfRegistered<{Module}DbContext>(sp, featureMigrations);` line
   alongside the six existing feature contexts, or your integration tests run against a database with
   no schema for your module.

---

## Step 10: Add Tests

Create a test project at `api/tests/Modules/{Module}/Wallow.{Module}.Tests/`. Use NSubstitute for mocking and AwesomeAssertions for assertions.

Then add a `case` arm for the module to `resolve_filter()` in `scripts/run-tests.sh`:

```bash
{module})       echo "$REPO_ROOT/api/tests/Modules/{Module}/Wallow.{Module}.Tests" ;;
```

The `case` is a hardcoded lookup with a default arm that passes the argument straight to `dotnet test`
as a path, so without this step `./scripts/run-tests.sh {module}` fails to resolve the project rather
than running your tests. Once the arm exists, run:

```bash
./scripts/run-tests.sh {module}
```

where `{module}` is the lowercase module name.

---

## Step 11: Inter-Module Communication

### Publishing Integration Events

Define integration events in `api/src/Shared/Wallow.Shared.Contracts/{Module}/Events/`. Integration events use primitive types (not strongly-typed IDs) so consuming modules have no domain dependencies:

```csharp
// api/src/Shared/Wallow.Shared.Contracts/{Module}/Events/{Entity}CreatedEvent.cs
public sealed record {Entity}CreatedEvent : IntegrationEvent
{
    public required Guid {Entity}Id { get; init; }
    public required string Name { get; init; }
}
```

### Consuming Events from Other Modules

Create Wolverine handlers in your Application layer that reference events from `Shared.Contracts`:

```csharp
public static class SomeExternalEventHandler
{
    public static async Task HandleAsync(
        SomeExternalEvent evt,
        I{LocalService} service,
        CancellationToken cancellationToken)
    {
        await service.ProcessAsync(evt, cancellationToken);
    }
}
```

Modules must never reference each other directly. All cross-module communication goes through `Shared.Contracts` events and Wolverine's in-memory message bus.

---

## Shared Infrastructure

These cross-cutting capabilities in the Shared layer are available to all modules:

All of these live under `api/src/Shared/`.

| Capability | Location | Description |
|------------|----------|-------------|
| Auditing | `Shared.Infrastructure.Core/Auditing/` | EF Core `SaveChangesInterceptor` for entity change audits |
| Caching | `Shared.Infrastructure.Core/Cache/` | Cache abstractions over Valkey (the `Redis` names in config keys and .NET APIs are Valkey's Redis-compatible protocol) |
| Messaging | `Shared.Infrastructure.Core/Messaging/` | Wolverine middleware and message conventions |
| Persistence | `Shared.Infrastructure.Core/Persistence/` | Shared EF Core conventions, interceptors, and migration helpers |
| Resilience | `Shared.Infrastructure.Core/Resilience/` | Shared retry and timeout policies |
| Background Jobs | `Shared.Kernel/BackgroundJobs/` (interface), `Shared.Infrastructure.BackgroundJobs/` (Hangfire implementation) | `IJobScheduler` abstraction over Hangfire |
| Plugins | `Shared.Infrastructure.Plugins/` | Isolated loading and lifecycle for plugin assemblies |
| AsyncAPI | `Shared.Infrastructure/AsyncApi/` | AsyncAPI document generation for the event catalog |
| Settings | `Shared.Infrastructure/Settings/` | Shared strongly-typed settings classes |
| API helpers | `Shared.Api/` | Shared controller extensions, settings, and health checks |

---

## Common Mistakes

| Mistake | Correct Approach |
|---------|------------------|
| Direct cross-module references | Use `Shared.Contracts` events only |
| Api referencing its own Infrastructure | Api references Application and `Shared.Api`; DI wiring is in Infrastructure. Identity is the one existing exception — do not copy it |
| Module extensions in Api layer | Put `{Module}ModuleExtensions.cs` in `Infrastructure/Extensions/` |
| PascalCase column names | Always use `.HasColumnName("snake_case")` |
| Inline ID conversion | Use `StronglyTypedIdConverter<TId>()` |
| Hand-rolling tenant query filters | Extend `TenantAwareDbContext<TContext>` and call `ApplyTenantQueryFilters(modelBuilder)` |
| Migrating in `Initialize{Module}ModuleAsync` | Register the context in `Wallow.MigrationService` and in `RunTestMigrationsAsync` |
| Missing TenantId index | Always index the `tenant_id` column |
| Domain events not bridged | Create handlers that translate domain events to integration events |
| Forgetting `WallowModules.cs` | Add both `Add{Module}Module` and `Initialize{Module}ModuleAsync` calls |
| Missing design-time factory | Required for `dotnet ef migrations` commands |
| Missing feature flag | Add `Modules.{Module}` to `appsettings.json` `FeatureManagement` |

---

## Pre-PR Checklist

- [ ] All 4 projects created with correct naming and added to solution
- [ ] Project references follow dependency rules
- [ ] Strongly-typed IDs implement `IStronglyTypedId<T>`
- [ ] Entities implement `ITenantScoped` (if tenant-scoped)
- [ ] Entities use factory methods, not public constructors
- [ ] Domain events raised in entity methods
- [ ] DbContext extends `TenantAwareDbContext<TContext>`, sets a lowercase schema, and calls `ApplyTenantQueryFilters`
- [ ] Entity configurations use `StronglyTypedIdConverter<T>` and snake_case columns
- [ ] Enum properties pair `.HasConversion<string>()` with an explicit `.HasMaxLength(…)` — never persisted as ints
- [ ] TenantId column is indexed
- [ ] DomainEvents property is ignored in entity configurations
- [ ] Repository implements interface from Application layer
- [ ] `Commands/` and `Queries/` handlers use the primary constructor pattern; `EventHandlers/` are static
- [ ] Every Infrastructure type a handler can inject is `public` — `ServiceLocationPolicy.NotAllowed` turns a non-public concrete type into a codegen failure on the first message
- [ ] Controllers are `partial` (required to host `[LoggerMessage]` and source-generated regex)
- [ ] Logging goes through `[LoggerMessage]`, not interpolated logger calls
- [ ] No `var` — every declaration writes its explicit type (`TreatWarningsAsErrors` makes this a build error)
- [ ] `dotnet format` run over the solution
- [ ] Application extensions register validators
- [ ] Module extensions in Infrastructure layer
- [ ] Module registered in `WallowModules.cs` with feature flag
- [ ] Feature flag added to `appsettings.json`
- [ ] Initial migration created, and the context registered in both `Wallow.MigrationService` and `RunTestMigrationsAsync`
- [ ] `case` arm added to `resolve_filter()` in `scripts/run-tests.sh`
- [ ] Tests pass via `./scripts/run-tests.sh`
- [ ] No direct cross-module references

---

*Reference implementation: the Inquiries module (`api/src/Modules/Inquiries/`) — with the
command-handler caveat noted in [Step 4](#handler-shape--the-folder-decides). Current modules:
Identity, Storage, Notifications, Announcements, Inquiries, ApiKeys, Branding.*

## Related Documentation

- [Messaging](messaging.md) — Wolverine, integration events, and `Shared.Contracts`
- [Authorization](authorization.md) — permissions and the `[HasPermission]` attribute
- [Background Jobs](background-jobs.md) — `IJobScheduler` and Hangfire
- [API Development](../development/api-development.md) — controller and endpoint conventions
- [Database Migrations](../development/database-migrations.md) — how migrations are applied
- [Architecture Assessment](assessment.md) — a point-in-time review of the existing modules
