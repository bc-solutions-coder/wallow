# Module Creation Guide

The definitive step-by-step guide for adding a new module to Wallow.

---

## Prerequisites

Before creating a new module:

- [ ] Understand Clean Architecture layers (Domain, Application, Infrastructure, Api)
- [ ] Review the [Billing module](/src/Modules/Billing/) as the reference implementation
- [ ] Decide on your module name
- [ ] Identify primary entities and their relationships
- [ ] Determine if the module needs database persistence (EF Core) or is stateless

> **Current modules:** Identity, Billing, Storage, Notifications, Messaging, Announcements, Inquiries. New modules should complement these existing capabilities.

---

## Quick Start Checklist

| Step | Action | Files Created |
|------|--------|---------------|
| 1 | Create project structure | 4 `.csproj` files |
| 2 | Configure project references | Update all 4 `.csproj` files |
| 3 | Create Domain layer | IDs, Entities, Events |
| 4 | Create Application layer | Commands, Queries, Handlers, Interfaces |
| 5 | Create Infrastructure layer | DbContext, Configurations, Repositories, Extensions |
| 6 | Create API layer | Controllers, Contracts |
| 7 | Register in WallowModules.cs | Update `WallowModules.cs` |
| 8 | Create database migration | Run `dotnet ef migrations add` |
| 9 | Add tests | Unit and integration tests |
| 10 | Add inter-module communication | Events in `Shared.Contracts` |

---

## Step 1: Create Project Structure

Create 4 class library projects following the naming convention `Wallow.{ModuleName}.{Layer}`:

```bash
# Create module directory
mkdir -p src/Modules/{ModuleName}

# Create projects (from solution root)
dotnet new classlib -n Wallow.{ModuleName}.Domain -o src/Modules/{ModuleName}/Wallow.{ModuleName}.Domain
dotnet new classlib -n Wallow.{ModuleName}.Application -o src/Modules/{ModuleName}/Wallow.{ModuleName}.Application
dotnet new classlib -n Wallow.{ModuleName}.Infrastructure -o src/Modules/{ModuleName}/Wallow.{ModuleName}.Infrastructure
dotnet new classlib -n Wallow.{ModuleName}.Api -o src/Modules/{ModuleName}/Wallow.{ModuleName}.Api

# Add to solution
dotnet sln add src/Modules/{ModuleName}/**/*.csproj
```

**Directory structure:**

```
src/Modules/{ModuleName}/
├── Wallow.{ModuleName}.Domain/
│   ├── Identity/              # Strongly-typed IDs
│   ├── Entities/              # Domain entities/aggregates
│   ├── Enums/                 # Enumerations
│   ├── Events/                # Domain events
│   ├── ValueObjects/          # (optional) Value objects
│   └── Exceptions/            # (optional) Custom exceptions
│
├── Wallow.{ModuleName}.Application/
│   ├── Commands/              # CQRS command handlers
│   ├── Queries/               # CQRS query handlers
│   ├── DTOs/                  # Data transfer objects
│   ├── Interfaces/            # Repository contracts
│   ├── EventHandlers/         # Domain-to-integration event bridges
│   ├── Extensions/            # Application layer DI registration
│   ├── Mappings/              # (optional) Entity-to-DTO mappings
│   └── Validators/            # (optional) FluentValidation
│
├── Wallow.{ModuleName}.Infrastructure/
│   ├── Extensions/            # DI registration
│   ├── Persistence/           # DbContext, repositories
│   │   ├── Configurations/    # Entity configurations
│   │   └── Repositories/      # Repository implementations
│   └── Migrations/            # EF Core migrations
│
└── Wallow.{ModuleName}.Api/
    ├── Controllers/           # API endpoints
    ├── Contracts/             # Request/Response DTOs
    └── Extensions/            # (optional) API-layer utilities like ResultExtensions
```

---

## Step 2: Configure Project References

### Domain (zero external dependencies)

```xml
<!-- Wallow.{ModuleName}.Domain.csproj -->
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <RootNamespace>Wallow.{ModuleName}.Domain</RootNamespace>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\..\Shared\Wallow.Shared.Kernel\Wallow.Shared.Kernel.csproj" />
  </ItemGroup>

</Project>
```

### Application (depends on Domain)

```xml
<!-- Wallow.{ModuleName}.Application.csproj -->
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <RootNamespace>Wallow.{ModuleName}.Application</RootNamespace>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\Wallow.{ModuleName}.Domain\Wallow.{ModuleName}.Domain.csproj" />
    <ProjectReference Include="..\..\..\Shared\Wallow.Shared.Kernel\Wallow.Shared.Kernel.csproj" />
    <ProjectReference Include="..\..\..\Shared\Wallow.Shared.Contracts\Wallow.Shared.Contracts.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="FluentValidation" />
    <PackageReference Include="FluentValidation.DependencyInjectionExtensions" />
  </ItemGroup>

</Project>
```

### Infrastructure (implements Application interfaces)

```xml
<!-- Wallow.{ModuleName}.Infrastructure.csproj -->
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <RootNamespace>Wallow.{ModuleName}.Infrastructure</RootNamespace>
  </PropertyGroup>

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
    <ProjectReference Include="..\Wallow.{ModuleName}.Domain\Wallow.{ModuleName}.Domain.csproj" />
    <ProjectReference Include="..\Wallow.{ModuleName}.Application\Wallow.{ModuleName}.Application.csproj" />
    <ProjectReference Include="..\..\..\Shared\Wallow.Shared.Infrastructure\Wallow.Shared.Infrastructure.csproj" />
  </ItemGroup>

</Project>
```

### Api (depends on Application only)

```xml
<!-- Wallow.{ModuleName}.Api.csproj -->
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <RootNamespace>Wallow.{ModuleName}.Api</RootNamespace>
  </PropertyGroup>

  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\Wallow.{ModuleName}.Application\Wallow.{ModuleName}.Application.csproj" />
  </ItemGroup>

</Project>
```

> **Note:** The Api layer does NOT reference Infrastructure. Module registration is done via Infrastructure extensions, and DI wiring is handled by `WallowModules.cs`.

---

## Step 3: Create Domain Layer

### 3.1 Strongly-Typed ID

```csharp
// Identity/{Entity}Id.cs
using Wallow.Shared.Kernel.Identity;

namespace Wallow.{ModuleName}.Domain.Identity;

public readonly record struct {Entity}Id(Guid Value) : IStronglyTypedId<{Entity}Id>
{
    public static {Entity}Id Create(Guid value) => new(value);
    public static {Entity}Id New() => new(Guid.NewGuid());
}
```

### 3.2 Domain Entity

```csharp
// Entities/{Entity}.cs
using Wallow.{ModuleName}.Domain.Events;
using Wallow.{ModuleName}.Domain.Identity;
using Wallow.Shared.Kernel.Domain;
using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.MultiTenancy;

namespace Wallow.{ModuleName}.Domain.Entities;

/// <summary>
/// {Entity} aggregate root.
/// </summary>
public sealed class {Entity} : AggregateRoot<{Entity}Id>, ITenantScoped
{
    public TenantId TenantId { get; set; }
    public string Name { get; private set; } = string.Empty;
    // Add other properties...

    private {Entity}() { } // EF Core constructor

    private {Entity}(string name)
    {
        Id = {Entity}Id.New();
        Name = name;
    }

    public static {Entity} Create(string name, Guid createdByUserId)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new BusinessRuleException("{ModuleName}.NameRequired", "Name cannot be empty");

        var entity = new {Entity}(name);
        entity.SetCreated(createdByUserId);

        entity.RaiseDomainEvent(new {Entity}CreatedDomainEvent(entity.Id.Value, name));

        return entity;
    }

    public void Update(string name, Guid updatedByUserId)
    {
        Name = name;
        SetUpdated(updatedByUserId);
    }
}
```

### 3.3 Domain Event

```csharp
// Events/{Entity}CreatedDomainEvent.cs
using Wallow.Shared.Kernel.Domain;

namespace Wallow.{ModuleName}.Domain.Events;

public sealed record {Entity}CreatedDomainEvent(
    Guid {Entity}Id,
    string Name
) : DomainEvent;
```

---

## Step 4: Create Application Layer

### 4.1 Command

```csharp
// Commands/Create{Entity}/Create{Entity}Command.cs
namespace Wallow.{ModuleName}.Application.Commands.Create{Entity};

public sealed record Create{Entity}Command(
    string Name
    // Add other properties as needed
);
```

### 4.2 Handler (Primary Constructor Pattern)

Wolverine supports multiple handler patterns. The primary constructor pattern is preferred for most handlers:

```csharp
// Commands/Create{Entity}/Create{Entity}Handler.cs
using Wallow.{ModuleName}.Application.DTOs;
using Wallow.{ModuleName}.Application.Interfaces;
using Wallow.{ModuleName}.Application.Mappings;
using Wallow.{ModuleName}.Domain.Entities;
using Wallow.Shared.Kernel.Results;
using Wolverine;

namespace Wallow.{ModuleName}.Application.Commands.Create{Entity};

public sealed class Create{Entity}Handler(
    I{Entity}Repository repository,
    IMessageBus messageBus)
{
    public async Task<Result<{Entity}Dto>> Handle(
        Create{Entity}Command command,
        CancellationToken cancellationToken)
    {
        // Validation
        var exists = await repository.ExistsByNameAsync(command.Name, cancellationToken);
        if (exists)
        {
            return Result.Failure<{Entity}Dto>(
                Error.Conflict($"{Entity} '{command.Name}' already exists"));
        }

        // Create entity
        var entity = {Entity}.Create(
            command.Name,
            command.UserId);

        // Persist
        repository.Add(entity);
        await repository.SaveChangesAsync(cancellationToken);

        return Result.Success(entity.ToDto());
    }
}
```

### 4.3 Query

```csharp
// Queries/Get{Entity}ById/Get{Entity}ByIdQuery.cs
namespace Wallow.{ModuleName}.Application.Queries.Get{Entity}ById;

public sealed record Get{Entity}ByIdQuery(Guid Id);
```

```csharp
// Queries/Get{Entity}ById/Get{Entity}ByIdHandler.cs
using Wallow.{ModuleName}.Application.DTOs;
using Wallow.{ModuleName}.Application.Interfaces;
using Wallow.{ModuleName}.Application.Mappings;
using Wallow.{ModuleName}.Domain.Identity;
using Wallow.Shared.Kernel.Results;

namespace Wallow.{ModuleName}.Application.Queries.Get{Entity}ById;

public sealed class Get{Entity}ByIdHandler(I{Entity}Repository repository)
{
    public async Task<Result<{Entity}Dto>> Handle(
        Get{Entity}ByIdQuery query,
        CancellationToken cancellationToken)
    {
        var entity = await repository.GetByIdAsync(
            {Entity}Id.Create(query.Id), cancellationToken);

        if (entity is null)
        {
            return Result.Failure<{Entity}Dto>(
                Error.NotFound($"{Entity} with ID '{query.Id}' not found"));
        }

        return Result.Success(entity.ToDto());
    }
}
```

### 4.4 Repository Interface

```csharp
// Interfaces/I{Entity}Repository.cs
using Wallow.{ModuleName}.Domain.Entities;
using Wallow.{ModuleName}.Domain.Identity;

namespace Wallow.{ModuleName}.Application.Interfaces;

public interface I{Entity}Repository
{
    Task<{Entity}?> GetByIdAsync({Entity}Id id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<{Entity}>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<bool> ExistsByNameAsync(string name, CancellationToken cancellationToken = default);
    void Add({Entity} entity);
    void Update({Entity} entity);
    void Remove({Entity} entity);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
```

### 4.5 DTO

```csharp
// DTOs/{Entity}Dto.cs
namespace Wallow.{ModuleName}.Application.DTOs;

public sealed record {Entity}Dto(
    Guid Id,
    string Name,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);
```

### 4.6 Mappings

```csharp
// Mappings/{Entity}Mappings.cs
using Wallow.{ModuleName}.Application.DTOs;
using Wallow.{ModuleName}.Domain.Entities;

namespace Wallow.{ModuleName}.Application.Mappings;

public static class {Entity}Mappings
{
    public static {Entity}Dto ToDto(this {Entity} entity) => new(
        entity.Id.Value,
        entity.Name,
        entity.CreatedAt,
        entity.UpdatedAt
    );
}
```

### 4.7 Validator (Optional)

```csharp
// Commands/Create{Entity}/Create{Entity}Validator.cs
using FluentValidation;

namespace Wallow.{ModuleName}.Application.Commands.Create{Entity};

public sealed class Create{Entity}Validator : AbstractValidator<Create{Entity}Command>
{
    public Create{Entity}Validator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(200);
    }
}
```

### 4.8 Domain Event Handler (Bridge to Integration Event)

```csharp
// EventHandlers/{Entity}CreatedDomainEventHandler.cs
using Wallow.{ModuleName}.Domain.Events;
using Wallow.{ModuleName}.Application.Interfaces;
using Wallow.{ModuleName}.Domain.Identity;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace Wallow.{ModuleName}.Application.EventHandlers;

public sealed class {Entity}CreatedDomainEventHandler
{
    public static async Task HandleAsync(
        {Entity}CreatedDomainEvent domainEvent,
        I{Entity}Repository repository,
        IMessageBus bus,
        ILogger<{Entity}CreatedDomainEventHandler> logger,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Handling {Entity}CreatedDomainEvent for {Entity} {Id}",
            domainEvent.{Entity}Id);

        // Enrich with additional data if needed
        var entity = await repository.GetByIdAsync(
            {Entity}Id.Create(domainEvent.{Entity}Id), cancellationToken);

        // Publish integration event for other modules
        await bus.PublishAsync(new Wallow.Shared.Contracts.{ModuleName}.Events.{Entity}CreatedEvent
        {
            {Entity}Id = domainEvent.{Entity}Id,
            Name = domainEvent.Name,
            // Map other properties...
        });

        logger.LogInformation(
            "Published {Entity}CreatedEvent for {Entity} {Id}",
            domainEvent.{Entity}Id);
    }
}
```

---

## Step 5: Create Infrastructure Layer

### 5.1 DbContext

```csharp
// Persistence/{ModuleName}DbContext.cs
using Wallow.{ModuleName}.Domain.Entities;
using Wallow.Shared.Kernel.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace Wallow.{ModuleName}.Infrastructure.Persistence;

public sealed class {ModuleName}DbContext : DbContext
{
    private readonly ITenantContext _tenantContext;

    public DbSet<{Entity}> {Entities} => Set<{Entity}>();

    public {ModuleName}DbContext(
        DbContextOptions<{ModuleName}DbContext> options,
        ITenantContext tenantContext) : base(options)
    {
        _tenantContext = tenantContext;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Set schema (lowercase, no underscores)
        modelBuilder.HasDefaultSchema("{modulename}");

        // Apply configurations from assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof({ModuleName}DbContext).Assembly);

        // Apply tenant query filters for all ITenantScoped entities
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(ITenantScoped).IsAssignableFrom(entityType.ClrType))
            {
                var parameter = System.Linq.Expressions.Expression.Parameter(entityType.ClrType, "e");
                var property = System.Linq.Expressions.Expression.Property(parameter, nameof(ITenantScoped.TenantId));
                var tenantId = System.Linq.Expressions.Expression.Property(
                    System.Linq.Expressions.Expression.Constant(_tenantContext),
                    nameof(ITenantContext.TenantId));
                var equals = System.Linq.Expressions.Expression.Equal(property, tenantId);
                var lambda = System.Linq.Expressions.Expression.Lambda(equals, parameter);

                modelBuilder.Entity(entityType.ClrType).HasQueryFilter(lambda);
            }
        }
    }
}
```

### 5.2 Entity Configuration

```csharp
// Persistence/Configurations/{Entity}Configuration.cs
using Wallow.{ModuleName}.Domain.Entities;
using Wallow.{ModuleName}.Domain.Identity;
using Wallow.Shared.Kernel.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Wallow.{ModuleName}.Infrastructure.Persistence.Configurations;

public sealed class {Entity}Configuration : IEntityTypeConfiguration<{Entity}>
{
    public void Configure(EntityTypeBuilder<{Entity}> builder)
    {
        // Table name (lowercase, plural)
        builder.ToTable("{entities}");

        // Primary key
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .HasConversion(new StronglyTypedIdConverter<{Entity}Id>())
            .HasColumnName("id")
            .ValueGeneratedNever();

        // TenantId (required for multi-tenancy)
        builder.Property(e => e.TenantId)
            .HasConversion(id => id.Value, value => TenantId.Create(value))
            .HasColumnName("tenant_id")
            .IsRequired();

        // Properties (all snake_case)
        builder.Property(e => e.Name)
            .HasColumnName("name")
            .HasMaxLength(200)
            .IsRequired();

        // Audit fields
        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(e => e.UpdatedAt)
            .HasColumnName("updated_at");

        builder.Property(e => e.CreatedBy)
            .HasColumnName("created_by");

        builder.Property(e => e.UpdatedBy)
            .HasColumnName("updated_by");

        // Ignore domain events (not persisted)
        builder.Ignore(e => e.DomainEvents);

        // Indexes
        builder.HasIndex(e => e.TenantId);
        builder.HasIndex(e => e.Name);
    }
}
```

### 5.3 Repository Implementation

```csharp
// Persistence/Repositories/{Entity}Repository.cs
using Wallow.{ModuleName}.Application.Interfaces;
using Wallow.{ModuleName}.Domain.Entities;
using Wallow.{ModuleName}.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Wallow.{ModuleName}.Infrastructure.Persistence.Repositories;

public sealed class {Entity}Repository : I{Entity}Repository
{
    private readonly {ModuleName}DbContext _context;

    public {Entity}Repository({ModuleName}DbContext context)
    {
        _context = context;
    }

    public async Task<{Entity}?> GetByIdAsync({Entity}Id id, CancellationToken cancellationToken = default)
    {
        return await _context.{Entities}.FindAsync([id], cancellationToken);
    }

    public async Task<IReadOnlyList<{Entity}>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.{Entities}
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> ExistsByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        return await _context.{Entities}
            .AnyAsync(e => e.Name == name, cancellationToken);
    }

    public void Add({Entity} entity)
    {
        _context.{Entities}.Add(entity);
    }

    public void Update({Entity} entity)
    {
        _context.{Entities}.Update(entity);
    }

    public void Remove({Entity} entity)
    {
        _context.{Entities}.Remove(entity);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
```

### 5.4 Design-Time Factory (for EF Migrations)

```csharp
// Persistence/{ModuleName}DbContextFactory.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Wallow.{ModuleName}.Infrastructure.Persistence;

/// <summary>
/// Design-time factory for {ModuleName}DbContext to enable EF Core migrations.
/// Only used at design-time by dotnet ef commands.
/// </summary>
public class {ModuleName}DbContextFactory : IDesignTimeDbContextFactory<{ModuleName}DbContext>
{
    public {ModuleName}DbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<{ModuleName}DbContext>();

        // Use a placeholder connection string for design-time
        optionsBuilder.UseNpgsql("Host=localhost;Database=wallow;Username=postgres;Password=postgres");

        // Create a mock tenant context for design-time
        var mockTenantContext = new DesignTimeTenantContext();

        return new {ModuleName}DbContext(optionsBuilder.Options, mockTenantContext);
    }
}
```

### 5.5 Design-Time Tenant Context

```csharp
// Persistence/DesignTimeTenantContext.cs
using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.MultiTenancy;

namespace Wallow.{ModuleName}.Infrastructure.Persistence;

/// <summary>
/// Mock ITenantContext for design-time migrations.
/// Returns a placeholder TenantId that is never used at runtime.
/// </summary>
internal sealed class DesignTimeTenantContext : ITenantContext
{
    public TenantId TenantId => new(Guid.Parse("00000000-0000-0000-0000-000000000000"));
    public string TenantName => "design-time";
    public bool IsResolved => true;

    public void SetTenant(TenantId tenantId, string tenantName = "")
    {
        // No-op for design-time
    }

    public void Clear()
    {
        // No-op for design-time
    }
}
```

### 5.6 Application Extensions

First, create the Application layer extension for validator registration:

```csharp
// Application/Extensions/ApplicationExtensions.cs
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Wallow.{ModuleName}.Application.Extensions;

public static class ApplicationExtensions
{
    public static IServiceCollection Add{ModuleName}Application(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(ApplicationExtensions).Assembly);
        return services;
    }
}
```

### 5.7 Infrastructure Extensions

```csharp
// Infrastructure/Extensions/{ModuleName}InfrastructureExtensions.cs
using Wallow.{ModuleName}.Application.Interfaces;
using Wallow.{ModuleName}.Infrastructure.Persistence;
using Wallow.{ModuleName}.Infrastructure.Persistence.Repositories;
using Wallow.Shared.Kernel.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Wallow.{ModuleName}.Infrastructure.Extensions;

public static class {ModuleName}InfrastructureExtensions
{
    public static IServiceCollection Add{ModuleName}Infrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.Add{ModuleName}Persistence(configuration);

        return services;
    }

    private static IServiceCollection Add{ModuleName}Persistence(
        this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        services.AddDbContext<{ModuleName}DbContext>((sp, options) =>
        {
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "{modulename}");
            });
            options.AddInterceptors(sp.GetRequiredService<TenantSaveChangesInterceptor>());
        });

        // Register repositories
        services.AddScoped<I{Entity}Repository, {Entity}Repository>();

        return services;
    }
}
```

---

## Step 6: Create API Layer

### 6.1 Module Extensions (in Infrastructure layer)

Module extensions live in the **Infrastructure** layer, not Api. This keeps the Api layer focused on controllers and contracts:

```csharp
// Infrastructure/Extensions/{ModuleName}ModuleExtensions.cs
using Wallow.{ModuleName}.Application.Extensions;
using Wallow.{ModuleName}.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Wallow.{ModuleName}.Infrastructure.Extensions;

public static class {ModuleName}ModuleExtensions
{
    public static IServiceCollection Add{ModuleName}Module(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Add{ModuleName}Application();
        services.Add{ModuleName}Infrastructure(configuration);
        return services;
    }

    public static async Task<WebApplication> Initialize{ModuleName}ModuleAsync(
        this WebApplication app)
    {
        try
        {
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<{ModuleName}DbContext>();
            await db.Database.MigrateAsync();
        }
        catch (Exception ex)
        {
            var logger = app.Services.GetRequiredService<ILoggerFactory>()
                .CreateLogger("{ModuleName}Module");
            logger.LogWarning(ex, "{ModuleName} module startup failed. Ensure PostgreSQL is running.");
        }

        return app;
    }
}
```

### 6.2 Controller

```csharp
// Controllers/{Entities}Controller.cs
using Wallow.{ModuleName}.Api.Contracts;
using Wallow.{ModuleName}.Application.Commands.Create{Entity};
using Wallow.{ModuleName}.Application.DTOs;
using Wallow.{ModuleName}.Application.Queries.Get{Entity}ById;
using Wallow.{ModuleName}.Application.Queries.GetAll{Entities};
using Wallow.Shared.Kernel.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace Wallow.{ModuleName}.Api.Controllers;

[ApiController]
[Route("api/{modulename}/{entities}")]
[Authorize]
[Tags("{Entities}")]
[Produces("application/json")]
[Consumes("application/json")]
public class {Entities}Controller : ControllerBase
{
    private readonly IMessageBus _bus;

    public {Entities}Controller(IMessageBus bus)
    {
        _bus = bus;
    }

    /// <summary>
    /// Get all {entities}.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<{Entity}Response>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var result = await _bus.InvokeAsync<Result<IReadOnlyList<{Entity}Dto>>>(
            new GetAll{Entities}Query(), cancellationToken);

        return result.Map(items =>
            (IReadOnlyList<{Entity}Response>)items.Select(ToResponse).ToList())
            .ToActionResult();
    }

    /// <summary>
    /// Get a specific {entity} by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof({Entity}Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _bus.InvokeAsync<Result<{Entity}Dto>>(
            new Get{Entity}ByIdQuery(id), cancellationToken);

        return result.Map(ToResponse).ToActionResult();
    }

    /// <summary>
    /// Create a new {entity}.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof({Entity}Response), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        [FromBody] Create{Entity}Request request,
        CancellationToken cancellationToken)
    {
        var command = new Create{Entity}Command(request.Name);

        var result = await _bus.InvokeAsync<Result<{Entity}Dto>>(command, cancellationToken);

        return result.Match(
            onSuccess: dto => CreatedAtAction(nameof(GetById), new { id = dto.Id }, ToResponse(dto)),
            onFailure: error => result.ToActionResult()
        );
    }

    private static {Entity}Response ToResponse({Entity}Dto dto) => new(
        dto.Id,
        dto.Name,
        dto.CreatedAt,
        dto.UpdatedAt
    );
}
```

### 6.3 Request/Response Contracts

```csharp
// Contracts/Create{Entity}Request.cs
namespace Wallow.{ModuleName}.Api.Contracts;

public sealed record Create{Entity}Request(string Name);
```

```csharp
// Contracts/{Entity}Response.cs
namespace Wallow.{ModuleName}.Api.Contracts;

public sealed record {Entity}Response(
    Guid Id,
    string Name,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);
```

### 6.4 Result Extensions (if not shared)

```csharp
// Extensions/ResultExtensions.cs
using Wallow.Shared.Kernel.Results;
using Microsoft.AspNetCore.Mvc;

namespace Wallow.{ModuleName}.Api.Extensions;

public static class ResultExtensions
{
    public static IActionResult ToActionResult(this Result result)
    {
        if (result.IsSuccess)
            return new OkResult();

        return ToProblemDetails(result.Error);
    }

    public static IActionResult ToActionResult<T>(this Result<T> result)
    {
        if (result.IsSuccess)
            return new OkObjectResult(result.Value);

        return ToProblemDetails(result.Error);
    }

    public static IActionResult ToCreatedResult<T>(this Result<T> result, string? location)
    {
        if (result.IsSuccess)
            return new CreatedResult(location, result.Value);

        return ToProblemDetails(result.Error);
    }

    private static IActionResult ToProblemDetails(Error error) =>
        error.Type switch
        {
            ErrorType.NotFound => new NotFoundObjectResult(new ProblemDetails
            {
                Title = "Not Found",
                Detail = error.Message,
                Status = 404
            }),
            ErrorType.Conflict => new ConflictObjectResult(new ProblemDetails
            {
                Title = "Conflict",
                Detail = error.Message,
                Status = 409
            }),
            ErrorType.Validation => new BadRequestObjectResult(new ProblemDetails
            {
                Title = "Validation Error",
                Detail = error.Message,
                Status = 400
            }),
            _ => new ObjectResult(new ProblemDetails
            {
                Title = "Error",
                Detail = error.Message,
                Status = 500
            })
            { StatusCode = 500 }
        };
}
```

---

## Step 7: Register Module in WallowModules.cs

All modules are registered in `src/Wallow.Api/WallowModules.cs`. This provides centralized, explicit module management.

### 7.1 Add Using Statement

```csharp
using Wallow.{ModuleName}.Infrastructure.Extensions;
```

### 7.2 Register in AddWallowModules()

Add your module to the appropriate section based on its type:

```csharp
public static IServiceCollection AddWallowModules(
    this IServiceCollection services,
    IConfiguration configuration)
{
    services.AddSingleton(configuration);
    services.AddFeatureManagement();
    ServiceProvider tempProvider = services.BuildServiceProvider();
    IFeatureManager featureManager = tempProvider.GetRequiredService<IFeatureManager>();

    // PLATFORM MODULES - Core infrastructure services
    if (featureManager.IsEnabledAsync("Modules.Identity").GetAwaiter().GetResult())
        services.AddIdentityModule(configuration);

    if (featureManager.IsEnabledAsync("Modules.Billing").GetAwaiter().GetResult())
        services.AddBillingModule(configuration);

    if (featureManager.IsEnabledAsync("Modules.Notifications").GetAwaiter().GetResult())
        services.AddNotificationsModule(configuration);

    if (featureManager.IsEnabledAsync("Modules.Messaging").GetAwaiter().GetResult())
        services.AddMessagingModule(configuration);

    if (featureManager.IsEnabledAsync("Modules.Announcements").GetAwaiter().GetResult())
        services.AddAnnouncementsModule(configuration);

    if (featureManager.IsEnabledAsync("Modules.Storage").GetAwaiter().GetResult())
        services.AddStorageModule(configuration);

    // FEATURE MODULES - Higher-level application features
    if (featureManager.IsEnabledAsync("Modules.Inquiries").GetAwaiter().GetResult())
        services.AddInquiriesModule(configuration);

    // Add your module in the appropriate section:
    if (featureManager.IsEnabledAsync("Modules.{ModuleName}").GetAwaiter().GetResult())
        services.Add{ModuleName}Module(configuration);

    return services;
}
```

### 7.3 Register in InitializeWallowModulesAsync()

```csharp
public static async Task InitializeWallowModulesAsync(this WebApplication app)
{
    // ... existing modules ...

    await app.Initialize{ModuleName}ModuleAsync();
}
```

> **Note:** Wolverine handler discovery and RabbitMQ routing are automatic. Program.cs uses `UseConventionalRouting()` and discovers all handlers in `Wallow.*` assemblies automatically. No manual handler registration or RabbitMQ configuration is needed.

---

## Step 8: Create Database Migration

```bash
dotnet ef migrations add InitialCreate \
    --project src/Modules/{ModuleName}/Wallow.{ModuleName}.Infrastructure \
    --startup-project src/Wallow.Api \
    --context {ModuleName}DbContext
```

The migration will run automatically on startup via `Initialize{ModuleName}ModuleAsync()`.

---

## Step 9: Add Tests

### 9.1 Test Project Structure

```
tests/Modules/{ModuleName}/
├── {ModuleName}.Domain.Tests/        # Domain unit tests
├── {ModuleName}.Application.Tests/   # Handler unit tests
├── {ModuleName}.Architecture.Tests/  # Architecture validation
└── {ModuleName}.IntegrationTests/    # Full integration tests
```

### 9.2 Unit Test Example

```csharp
public class Create{Entity}HandlerTests
{
    private readonly I{Entity}Repository _repository;
    private readonly Create{Entity}Handler _handler;

    public Create{Entity}HandlerTests()
    {
        _repository = Substitute.For<I{Entity}Repository>();
        _handler = new Create{Entity}Handler(_repository);
    }

    [Fact]
    public async Task Handle_WithValidCommand_ShouldCreate{Entity}()
    {
        // Arrange
        var command = new Create{Entity}Command("Test Name");
        _repository.ExistsByNameAsync(command.Name, Arg.Any<CancellationToken>())
            .Returns(false);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _repository.Received(1).Add(Arg.Any<{Entity}>());
        await _repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithDuplicateName_ShouldReturnConflict()
    {
        // Arrange
        var command = new Create{Entity}Command("Existing Name");
        _repository.ExistsByNameAsync(command.Name, Arg.Any<CancellationToken>())
            .Returns(true);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.Conflict);
    }
}
```

### 9.3 Integration Test Example

```csharp
public class {Entities}ControllerTests : IClassFixture<WallowApiFactory>
{
    private readonly HttpClient _client;

    public {Entities}ControllerTests(WallowApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Create{Entity}_ShouldReturn201()
    {
        // Arrange
        var request = new Create{Entity}Request("Test {Entity}");

        // Act
        var response = await _client.PostAsJsonAsync("/api/{modulename}/{entities}", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }
}
```

---

## Step 10: Inter-Module Communication

### 10.1 Define Integration Event in Shared.Contracts

```csharp
// src/Shared/Wallow.Shared.Contracts/{ModuleName}/Events/{Entity}CreatedEvent.cs
namespace Wallow.Shared.Contracts.{ModuleName}.Events;

/// <summary>
/// Published when a {entity} is created.
/// Consumers: [list expected consumers]
/// </summary>
public sealed record {Entity}CreatedEvent : IntegrationEvent
{
    public required Guid {Entity}Id { get; init; }
    public required string Name { get; init; }
    // Add properties that consumers need (use primitive types, not strongly-typed IDs)
}
```

### 10.2 Consuming Events from Other Modules

```csharp
// Application/EventHandlers/SomeModuleEventHandler.cs
using Wallow.Shared.Contracts.SomeModule.Events;

namespace Wallow.{ModuleName}.Application.EventHandlers;

public static class SomeModuleEventHandler
{
    public static async Task HandleAsync(
        SomeEvent evt,
        I{SomeService} service,
        ILogger<SomeModuleEventHandler> logger,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Handling SomeEvent {EventId}", evt.EventId);

        // Process the event
        await service.ProcessAsync(evt, cancellationToken);
    }
}
```

---

## Patterns Reference

### Handler Pattern (Primary Constructor - Preferred)

```csharp
public sealed class {Command}Handler(
    IDependency dependency,
    IMessageBus messageBus)
{
    public async Task<Result<{Dto}>> Handle(
        {Command} command,
        CancellationToken cancellationToken)
    {
        // Implementation
    }
}
```

### DbContext with Tenant Filters

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.HasDefaultSchema("{modulename}");
    modelBuilder.ApplyConfigurationsFromAssembly(typeof({ModuleName}DbContext).Assembly);

    foreach (var entityType in modelBuilder.Model.GetEntityTypes())
    {
        if (typeof(ITenantScoped).IsAssignableFrom(entityType.ClrType))
        {
            var parameter = Expression.Parameter(entityType.ClrType, "e");
            var property = Expression.Property(parameter, nameof(ITenantScoped.TenantId));
            var tenantId = Expression.Property(Expression.Constant(_tenantContext), nameof(ITenantContext.TenantId));
            var equals = Expression.Equal(property, tenantId);
            var lambda = Expression.Lambda(equals, parameter);

            modelBuilder.Entity(entityType.ClrType).HasQueryFilter(lambda);
        }
    }
}
```

### Entity Configuration with StronglyTypedIdConverter and snake_case

```csharp
public void Configure(EntityTypeBuilder<{Entity}> builder)
{
    builder.ToTable("{entities}");

    builder.HasKey(e => e.Id);
    builder.Property(e => e.Id)
        .HasConversion(new StronglyTypedIdConverter<{Entity}Id>())
        .HasColumnName("id")
        .ValueGeneratedNever();

    builder.Property(e => e.TenantId)
        .HasConversion(id => id.Value, value => TenantId.Create(value))
        .HasColumnName("tenant_id")
        .IsRequired();

    builder.Property(e => e.Name)
        .HasColumnName("name")
        .HasMaxLength(200);

    builder.Property(e => e.CreatedAt)
        .HasColumnName("created_at");

    builder.Property(e => e.UpdatedAt)
        .HasColumnName("updated_at");

    builder.Ignore(e => e.DomainEvents);

    builder.HasIndex(e => e.TenantId);
}
```

---

## Common Mistakes to Avoid

Based on analysis of existing modules:

| Mistake | Correct Approach |
|---------|------------------|
| Direct cross-module references | Use `Shared.Contracts` events only |
| Api referencing Infrastructure | Api should only reference Application; module registration is in Infrastructure |
| Module extensions in Api layer | Put `{ModuleName}ModuleExtensions.cs` in Infrastructure/Extensions |
| PascalCase column names | Always use `.HasColumnName("snake_case")` |
| Inline ID conversion | Use `StronglyTypedIdConverter<TId>()` |
| Missing tenant query filters | Apply filters for all `ITenantScoped` entities |
| Missing TenantId index | Always index `tenant_id` column |
| Domain events not bridged | Create handlers that publish integration events |
| Forgetting to register in WallowModules.cs | Add both `Add{Module}Module` and `Initialize{Module}ModuleAsync` calls |
| Missing design-time DbContext factory | Required for `dotnet ef migrations` |

---

## Checklist Before PR

- [ ] All 4 projects created with correct naming
- [ ] Project references follow dependency rules (Api references Application only, not Infrastructure)
- [ ] Strongly-typed IDs implement `IStronglyTypedId<T>`
- [ ] Entities implement `ITenantScoped` (if tenant-scoped)
- [ ] Entities use factory methods (not public constructors)
- [ ] Domain events raised in entity methods
- [ ] DbContext sets lowercase schema
- [ ] DbContext applies tenant query filters
- [ ] Entity configurations use `StronglyTypedIdConverter<T>`
- [ ] All columns use snake_case naming
- [ ] TenantId column is indexed
- [ ] DomainEvents property is ignored
- [ ] Repository implements interface from Application layer
- [ ] Handlers use primary constructor pattern
- [ ] Validators use FluentValidation
- [ ] Application extensions created (`Add{Module}Application`)
- [ ] Infrastructure extensions created (`Add{Module}Infrastructure`)
- [ ] Module extensions in Infrastructure layer (`Add{Module}Module`, `Initialize{Module}ModuleAsync`)
- [ ] Module registered in WallowModules.cs (both `AddWallowModules` and `InitializeWallowModulesAsync`)
- [ ] Integration events defined in Shared.Contracts (if needed)
- [ ] Initial migration created and runs
- [ ] Unit tests for handlers
- [ ] Integration tests for controllers
- [ ] No cross-module direct references

---

*Based on the Billing module reference implementation. Current modules: Identity, Billing, Storage, Notifications, Messaging, Announcements, Inquiries.*
