# Module Creation Guide

Step-by-step guide for adding a new module to Wallow.

---

## Prerequisites

Before creating a new module:

- Understand Clean Architecture layers (Domain, Application, Infrastructure, Api)
- Review the [Inquiries module](https://github.com/bc-solutions-coder/wallow/tree/main/src/Modules/Inquiries) as a reference implementation
- Decide on your module name (PascalCase, singular noun)
- Identify primary entities and their relationships
- Determine if the module needs database persistence (EF Core) or is stateless

> **Current modules:** Identity, Branding, Storage, Notifications, Messaging, Announcements, Inquiries, ApiKeys. New modules should complement these existing capabilities.

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
mkdir -p src/Modules/{Module}

dotnet new classlib -n Wallow.{Module}.Domain -o src/Modules/{Module}/Wallow.{Module}.Domain
dotnet new classlib -n Wallow.{Module}.Application -o src/Modules/{Module}/Wallow.{Module}.Application
dotnet new classlib -n Wallow.{Module}.Infrastructure -o src/Modules/{Module}/Wallow.{Module}.Infrastructure
dotnet new classlib -n Wallow.{Module}.Api -o src/Modules/{Module}/Wallow.{Module}.Api

dotnet sln add src/Modules/{Module}/**/*.csproj
```

**Directory structure:**

```
src/Modules/{Module}/
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
</ItemGroup>
```

**Infrastructure** references Domain, Application, and `Shared.Infrastructure`:

```xml
<ItemGroup>
  <PackageReference Include="Dapper" />
  <PackageReference Include="Microsoft.EntityFrameworkCore" />
  <PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" />
  <PackageReference Include="Microsoft.EntityFrameworkCore.Design">
    <PrivateAssets>all</PrivateAssets>
    <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
  </PackageReference>
</ItemGroup>
<ItemGroup>
  <ProjectReference Include="..\Wallow.{Module}.Domain\Wallow.{Module}.Domain.csproj" />
  <ProjectReference Include="..\Wallow.{Module}.Application\Wallow.{Module}.Application.csproj" />
  <ProjectReference Include="..\..\..\Shared\Wallow.Shared.Infrastructure\Wallow.Shared.Infrastructure.csproj" />
</ItemGroup>
```

**Api** references only Application (not Infrastructure):

```xml
<ItemGroup>
  <FrameworkReference Include="Microsoft.AspNetCore.App" />
</ItemGroup>
<ItemGroup>
  <ProjectReference Include="..\Wallow.{Module}.Application\Wallow.{Module}.Application.csproj" />
</ItemGroup>
```

Module registration is handled by Infrastructure extensions called from `WallowModules.cs`, so the Api layer never needs to reference Infrastructure directly.

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

Each module owns its own PostgreSQL schema (lowercase module name). The DbContext must:
- Set a default schema via `modelBuilder.HasDefaultSchema("{modulename}")`
- Apply entity configurations from the assembly
- Apply tenant query filters for all `ITenantScoped` entities

The tenant query filter pattern uses expression trees to dynamically add `WHERE tenant_id = @currentTenantId` to all queries. See `InquiriesDbContext` for a reference implementation.

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

**`{Module}InfrastructureExtensions.cs`** registers the DbContext (with `TenantSaveChangesInterceptor`), repositories, and any module-specific services:

```csharp
services.AddDbContext<{Module}DbContext>((sp, options) =>
{
    options.UseNpgsql(connectionString, npgsql =>
    {
        npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "{modulename}");
    });
    options.AddInterceptors(sp.GetRequiredService<TenantSaveChangesInterceptor>());
});
```

**`{Module}ModuleExtensions.cs`** provides the two entry points called by `WallowModules.cs`:

```csharp
public static IServiceCollection Add{Module}Module(
    this IServiceCollection services, IConfiguration configuration)
{
    services.Add{Module}Application();
    services.Add{Module}Infrastructure(configuration);
    return services;
}

public static async Task<WebApplication> Initialize{Module}ModuleAsync(this WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<{Module}DbContext>();
    await db.Database.MigrateAsync();
    return app;
}
```

---

## Step 6: Create API Layer

### Controller

Controllers use Wolverine's `IMessageBus` to dispatch commands and queries. Follow these conventions:
- Route: `api/{modulename}/{entities}` (all lowercase)
- Annotate with `[Authorize]`, `[Tags("{Entities}")]`, `[Produces("application/json")]`
- Map Application DTOs to API-layer response records in the controller
- Use `Result<T>` from `Wallow.Shared.Kernel.Results` and convert with `.ToActionResult()`

### Request/Response Contracts

Define sealed records in the `Contracts/` folder. Keep them separate from Application DTOs so the API contract can evolve independently.

---

## Step 7: Register in WallowModules.cs

Edit `src/Wallow.Api/WallowModules.cs`:

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
    --project src/Modules/{Module}/Wallow.{Module}.Infrastructure \
    --startup-project src/Wallow.Api \
    --context {Module}DbContext
```

The migration runs automatically on startup via `Initialize{Module}ModuleAsync()`.

---

## Step 10: Add Tests

Create a test project at `tests/Modules/{Module}/Wallow.{Module}.Tests/`. Use NSubstitute for mocking and AwesomeAssertions for assertions.

Run tests with:

```bash
./scripts/run-tests.sh {module}
```

where `{module}` is the lowercase module name.

---

## Step 11: Inter-Module Communication

### Publishing Integration Events

Define integration events in `src/Shared/Wallow.Shared.Contracts/{Module}/Events/`. Integration events use primitive types (not strongly-typed IDs) so consuming modules have no domain dependencies:

```csharp
// src/Shared/Wallow.Shared.Contracts/{Module}/Events/{Entity}CreatedEvent.cs
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

| Capability | Location | Description |
|------------|----------|-------------|
| Auditing | `Shared.Infrastructure.Core/Auditing/` | EF Core `SaveChangesInterceptor` for entity change audits |
| Background Jobs | `Shared.Infrastructure.BackgroundJobs/` | `IJobScheduler` abstraction over Hangfire |
| Workflows | `Shared.Infrastructure.Workflows/` | Elsa 3 workflow engine integration |

---

## Common Mistakes

| Mistake | Correct Approach |
|---------|------------------|
| Direct cross-module references | Use `Shared.Contracts` events only |
| Api referencing Infrastructure | Api references Application only; DI wiring is in Infrastructure |
| Module extensions in Api layer | Put `{Module}ModuleExtensions.cs` in `Infrastructure/Extensions/` |
| PascalCase column names | Always use `.HasColumnName("snake_case")` |
| Inline ID conversion | Use `StronglyTypedIdConverter<TId>()` |
| Missing tenant query filters | Apply filters for all `ITenantScoped` entities |
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
- [ ] DbContext sets lowercase schema and applies tenant query filters
- [ ] Entity configurations use `StronglyTypedIdConverter<T>` and snake_case columns
- [ ] TenantId column is indexed
- [ ] DomainEvents property is ignored in entity configurations
- [ ] Repository implements interface from Application layer
- [ ] Handlers use primary constructor pattern
- [ ] Application extensions register validators
- [ ] Module extensions in Infrastructure layer
- [ ] Module registered in `WallowModules.cs` with feature flag
- [ ] Feature flag added to `appsettings.json`
- [ ] Initial migration created and runs successfully
- [ ] Tests pass via `./scripts/run-tests.sh`
- [ ] No direct cross-module references

---

*Reference implementation: [Inquiries module](https://github.com/bc-solutions-coder/wallow/tree/main/src/Modules/Inquiries). Current modules: Identity, Branding, Storage, Notifications, Messaging, Announcements, Inquiries, ApiKeys.*
