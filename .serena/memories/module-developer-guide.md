# Wallow Module Developer Guide

This guide explains how to create, configure, and maintain modules in the Wallow modular monolith. All modules should follow these patterns for consistency.

## Module Structure

Every module follows Clean Architecture with exactly 4 layers:

```
src/Modules/{ModuleName}/
├── Wallow.{ModuleName}.Domain/           # Domain layer
│   ├── Identity/                          # Strongly-typed IDs
│   ├── Entities/                          # Domain entities/aggregates
│   ├── Enums/                            # Enumerations
│   ├── Events/                           # Domain events
│   ├── ValueObjects/                     # (optional) Value objects
│   ├── Exceptions/                       # (optional) Custom exceptions
│   └── Projections/                      # (event-sourced only)
│
├── Wallow.{ModuleName}.Application/      # Application layer
│   ├── Commands/                         # CQRS command handlers
│   ├── Queries/                          # CQRS query handlers
│   ├── DTOs/                            # Data transfer objects
│   ├── Interfaces/                       # Service contracts
│   ├── EventHandlers/                    # Integration event consumers
│   ├── Mappings/                         # (optional) AutoMapper profiles
│   └── Validators/                       # (optional) FluentValidation
│
├── Wallow.{ModuleName}.Infrastructure/   # Infrastructure layer
│   ├── Extensions/                       # DI registration
│   ├── Persistence/                      # DbContext, repositories
│   ├── Migrations/                       # EF Core migrations
│   └── Services/                         # External service implementations
│
└── Wallow.{ModuleName}.Api/             # API layer
    ├── Controllers/                      # Endpoint handlers
    ├── Contracts/                        # Request/Response DTOs
    └── Extensions/                       # Module registration
```

## Creating a New Module

### Step 1: Create Project Structure

Create 4 class library projects with naming convention `Wallow.{ModuleName}.{Layer}`:

```bash
# Create module directories
mkdir -p src/Modules/{ModuleName}

# Create projects (from solution root)
dotnet new classlib -n Wallow.{ModuleName}.Domain -o src/Modules/{ModuleName}/Wallow.{ModuleName}.Domain
dotnet new classlib -n Wallow.{ModuleName}.Application -o src/Modules/{ModuleName}/Wallow.{ModuleName}.Application
dotnet new classlib -n Wallow.{ModuleName}.Infrastructure -o src/Modules/{ModuleName}/Wallow.{ModuleName}.Infrastructure
dotnet new classlib -n Wallow.{ModuleName}.Api -o src/Modules/{ModuleName}/Wallow.{ModuleName}.Api

# Add to solution
dotnet sln add src/Modules/{ModuleName}/**/*.csproj
```

### Step 2: Configure Project References

**Domain** (zero external dependencies):
```xml
<ItemGroup>
  <ProjectReference Include="..\..\..\Shared\Wallow.Shared.Kernel\Wallow.Shared.Kernel.csproj" />
</ItemGroup>
```

**Application** (depends on Domain):
```xml
<ItemGroup>
  <ProjectReference Include="..\Wallow.{ModuleName}.Domain\Wallow.{ModuleName}.Domain.csproj" />
  <ProjectReference Include="..\..\..\Shared\Wallow.Shared.Kernel\Wallow.Shared.Kernel.csproj" />
  <ProjectReference Include="..\..\..\Shared\Wallow.Shared.Contracts\Wallow.Shared.Contracts.csproj" />
</ItemGroup>
```

**Infrastructure** (implements Application interfaces):
```xml
<ItemGroup>
  <ProjectReference Include="..\Wallow.{ModuleName}.Application\Wallow.{ModuleName}.Application.csproj" />
  <ProjectReference Include="..\..\..\Shared\Wallow.Shared.Kernel\Wallow.Shared.Kernel.csproj" />
  <ProjectReference Include="..\..\..\Shared\Wallow.Shared.Contracts\Wallow.Shared.Contracts.csproj" />
</ItemGroup>
```

**Api** (depends on Application and Infrastructure):
```xml
<ItemGroup>
  <ProjectReference Include="..\Wallow.{ModuleName}.Application\Wallow.{ModuleName}.Application.csproj" />
  <ProjectReference Include="..\Wallow.{ModuleName}.Infrastructure\Wallow.{ModuleName}.Infrastructure.csproj" />
</ItemGroup>
```

### Step 3: Create Domain Layer

**Strongly-Typed ID** (Identity/{ModuleName}Id.cs):
```csharp
namespace Wallow.{ModuleName}.Domain.Identity;

public readonly record struct {Entity}Id(Guid Value)
{
    public static {Entity}Id Empty => new(Guid.Empty);
    public static {Entity}Id Create() => new(Guid.NewGuid());
    public static {Entity}Id Create(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}
```

**Domain Entity** (Entities/{Entity}.cs):
```csharp
namespace Wallow.{ModuleName}.Domain.Entities;

public sealed class {Entity} : Entity<{Entity}Id>, ITenantScoped
{
    public TenantId TenantId { get; private set; }
    // Properties...
    
    private {Entity}() { } // EF Core constructor
    
    public static {Entity} Create(TenantId tenantId, ...)
    {
        var entity = new {Entity}
        {
            Id = {Entity}Id.Create(),
            TenantId = tenantId,
            // Initialize...
        };
        entity.RaiseDomainEvent(new {Entity}CreatedDomainEvent(entity.Id, tenantId));
        return entity;
    }
}
```

**Domain Event** (Events/{Entity}CreatedDomainEvent.cs):
```csharp
namespace Wallow.{ModuleName}.Domain.Events;

public sealed record {Entity}CreatedDomainEvent({Entity}Id Id, TenantId TenantId) : DomainEvent;
```

### Step 4: Create Application Layer

**Command** (Commands/Create{Entity}/Create{Entity}Command.cs):
```csharp
namespace Wallow.{ModuleName}.Application.Commands.Create{Entity};

public sealed record Create{Entity}Command(...) : IRequest<Result<{Entity}Dto>>;
```

**Handler** (Commands/Create{Entity}/Create{Entity}Handler.cs):
```csharp
namespace Wallow.{ModuleName}.Application.Commands.Create{Entity};

public sealed class Create{Entity}Handler(
    I{Entity}Repository repository,
    ITenantContext tenantContext,
    IMessageBus bus)
{
    public async Task<Result<{Entity}Dto>> HandleAsync(
        Create{Entity}Command command,
        CancellationToken ct)
    {
        var entity = {Entity}.Create(tenantContext.TenantId, ...);
        await repository.AddAsync(entity, ct);
        await repository.SaveChangesAsync(ct);
        
        // Publish integration event
        await bus.PublishAsync(new {Entity}CreatedEvent { ... }, ct);
        
        return Result.Success(entity.ToDto());
    }
}
```

**Repository Interface** (Interfaces/I{Entity}Repository.cs):
```csharp
namespace Wallow.{ModuleName}.Application.Interfaces;

public interface I{Entity}Repository
{
    Task<{Entity}?> GetByIdAsync({Entity}Id id, CancellationToken ct = default);
    Task AddAsync({Entity} entity, CancellationToken ct = default);
    void Update({Entity} entity);
    void Remove({Entity} entity);
    Task SaveChangesAsync(CancellationToken ct = default);
}
```

### Step 5: Create Infrastructure Layer

**DbContext** (Persistence/{ModuleName}DbContext.cs):
```csharp
namespace Wallow.{ModuleName}.Infrastructure.Persistence;

public sealed class {ModuleName}DbContext : DbContext
{
    private readonly ITenantContext _tenantContext;
    
    public {ModuleName}DbContext(
        DbContextOptions<{ModuleName}DbContext> options,
        ITenantContext tenantContext)
        : base(options)
    {
        _tenantContext = tenantContext;
    }
    
    public DbSet<{Entity}> {Entities} => Set<{Entity}>();
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Use lowercase schema name (no underscores)
        modelBuilder.HasDefaultSchema("{modulename}");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof({ModuleName}DbContext).Assembly);
        
        // Apply tenant query filters for all ITenantScoped entities
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(ITenantScoped).IsAssignableFrom(entityType.ClrType))
            {
                var parameter = Expression.Parameter(entityType.ClrType, "e");
                var tenantProperty = Expression.Property(parameter, nameof(ITenantScoped.TenantId));
                var tenantValue = Expression.Constant(_tenantContext.TenantId);
                var filter = Expression.Lambda(Expression.Equal(tenantProperty, tenantValue), parameter);
                modelBuilder.Entity(entityType.ClrType).HasQueryFilter(filter);
            }
        }
    }
}
```

**Entity Configuration** (Persistence/Configurations/{Entity}Configuration.cs):
```csharp
namespace Wallow.{ModuleName}.Infrastructure.Persistence.Configurations;

public sealed class {Entity}Configuration : IEntityTypeConfiguration<{Entity}>
{
    public void Configure(EntityTypeBuilder<{Entity}> builder)
    {
        builder.ToTable("{entities}");  // lowercase, plural
        builder.HasKey(x => x.Id);
        
        builder.Property(x => x.Id)
            .HasConversion(new StronglyTypedIdConverter<{Entity}Id>())
            .HasColumnName("id")
            .ValueGeneratedNever();
            
        builder.Property(x => x.TenantId)
            .HasConversion(new StronglyTypedIdConverter<TenantId>())
            .HasColumnName("tenant_id")
            .IsRequired();
            
        // Indexes
        builder.HasIndex(x => x.TenantId);
    }
}
```

**Repository** (Persistence/Repositories/{Entity}Repository.cs):
```csharp
namespace Wallow.{ModuleName}.Infrastructure.Persistence.Repositories;

public sealed class {Entity}Repository : I{Entity}Repository
{
    private readonly {ModuleName}DbContext _context;
    
    public {Entity}Repository({ModuleName}DbContext context)
    {
        _context = context;
    }
    
    public async Task<{Entity}?> GetByIdAsync({Entity}Id id, CancellationToken ct = default)
        => await _context.{Entities}.FindAsync([id], ct);
    
    public Task AddAsync({Entity} entity, CancellationToken ct = default)
        => _context.{Entities}.AddAsync(entity, ct).AsTask();
    
    public void Update({Entity} entity) => _context.{Entities}.Update(entity);
    public void Remove({Entity} entity) => _context.{Entities}.Remove(entity);
    
    public Task SaveChangesAsync(CancellationToken ct = default)
        => _context.SaveChangesAsync(ct);
}
```

**Infrastructure Extensions** (Extensions/InfrastructureExtensions.cs):
```csharp
namespace Wallow.{ModuleName}.Infrastructure.Extensions;

public static class InfrastructureExtensions
{
    public static IServiceCollection Add{ModuleName}Infrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Add{ModuleName}Persistence(configuration);
        return services;
    }
    
    private static IServiceCollection Add{ModuleName}Persistence(
        this IServiceCollection services,
        IConfiguration configuration)
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
        
        services.AddScoped<I{Entity}Repository, {Entity}Repository>();
        
        return services;
    }
}
```

### Step 6: Create API Layer

**Module Extensions** (Extensions/{ModuleName}ModuleExtensions.cs):
```csharp
namespace Wallow.{ModuleName}.Api.Extensions;

public static class {ModuleName}ModuleExtensions
{
    public static IServiceCollection Add{ModuleName}Module(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Optional: Register validators
        // services.AddValidatorsFromAssembly(typeof({SomeValidator}).Assembly);
        
        services.Add{ModuleName}Infrastructure(configuration);
        return services;
    }
    
    public static async Task Use{ModuleName}ModuleAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<{ModuleName}DbContext>>();
        
        try
        {
            var db = scope.ServiceProvider.GetRequiredService<{ModuleName}DbContext>();
            await db.Database.MigrateAsync();
            logger.LogInformation("{ModuleName} module initialized");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "{ModuleName} initialization failed. Ensure PostgreSQL is running.");
        }
    }
}
```

**Controller** (Controllers/{Entity}Controller.cs):
```csharp
namespace Wallow.{ModuleName}.Api.Controllers;

[ApiController]
[Route("api/{modulename}/{entities}")]
[Authorize]
public class {Entity}Controller : ControllerBase
{
    private readonly IMessageBus _bus;
    
    public {Entity}Controller(IMessageBus bus) => _bus = bus;
    
    [HttpPost]
    public async Task<ActionResult<{Entity}Response>> Create(
        [FromBody] Create{Entity}Request request,
        CancellationToken ct)
    {
        var command = new Create{Entity}Command(...);
        var result = await _bus.InvokeAsync<Result<{Entity}Dto>>(command, ct);
        
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetById), new { id = result.Value.Id }, result.Value.ToResponse())
            : result.ToProblem();
    }
}
```

### Step 7: Register Module in Program.cs

Add to `src/Wallow.Api/Program.cs`:

```csharp
// 1. Register Wolverine handler assembly (around line 100)
opts.Discovery.IncludeAssembly(typeof(Wallow.{ModuleName}.Application.Commands.Create{Entity}.Create{Entity}Command).Assembly);

// 2. Configure RabbitMQ event publishing (around line 160)
opts.PublishMessage<{Entity}CreatedEvent>().ToRabbitExchange("{modulename}-events");

// 3. Configure RabbitMQ consumer queue (around line 200)
opts.ListenToRabbitQueue("{modulename}-inbox");

// 4. Register module services (around line 260)
builder.Services.Add{ModuleName}Module(builder.Configuration);

// 5. Initialize module (after app.Build(), around line 340)
await app.Use{ModuleName}ModuleAsync();
```

### Step 8: Create Migrations

```bash
dotnet ef migrations add InitialCreate \
    --project src/Modules/{ModuleName}/Wallow.{ModuleName}.Infrastructure \
    --startup-project src/Wallow.Api \
    --context {ModuleName}DbContext
```

## Inter-Module Communication

### Rule: NEVER reference other modules directly

Modules communicate ONLY through:
1. **Integration events** in `Shared.Contracts`
2. **Shared interfaces** in `Shared.Contracts` (like IDataExporter, IDataEraser)

### Publishing Integration Events

1. Define event in `Shared.Contracts/{ModuleName}/Events/`:

```csharp
namespace Wallow.Shared.Contracts.{ModuleName}.Events;

public sealed record {Entity}CreatedEvent : IntegrationEvent
{
    public required Guid Id { get; init; }
    public required Guid TenantId { get; init; }
    // Include all data consumers need
}
```

2. Publish from handler:
```csharp
await bus.PublishAsync(new {Entity}CreatedEvent
{
    Id = entity.Id.Value,
    TenantId = tenantContext.TenantId.Value,
    // ...
});
```

### Consuming Integration Events

Create handler in Application layer:

```csharp
namespace Wallow.{ModuleName}.Application.EventHandlers;

public static class SomeOtherModuleEventHandler
{
    public static async Task HandleAsync(
        SomeEvent evt,
        I{SomeService} service,
        CancellationToken ct)
    {
        // Process event
        await service.ProcessAsync(evt.Data, ct);
    }
}
```

## Testing

### Test Project Structure

```
tests/Modules/{ModuleName}/
├── {ModuleName}.Domain.Tests/        # Domain unit tests
├── {ModuleName}.Application.Tests/   # Handler unit tests
├── {ModuleName}.Infrastructure.Tests/ # Repository/service tests
├── {ModuleName}.Architecture.Tests/  # Architecture validation
└── Wallow.{ModuleName}.IntegrationTests/  # Full integration tests
```

### Unit Test Pattern

```csharp
public class Create{Entity}HandlerTests
{
    private readonly I{Entity}Repository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly IMessageBus _bus;
    private readonly Create{Entity}Handler _handler;
    
    public Create{Entity}HandlerTests()
    {
        _repository = Substitute.For<I{Entity}Repository>();
        _tenantContext = Substitute.For<ITenantContext>();
        _tenantContext.TenantId.Returns(new TenantId(Guid.NewGuid()));
        _bus = Substitute.For<IMessageBus>();
        _handler = new Create{Entity}Handler(_repository, _tenantContext, _bus);
    }
    
    [Fact]
    public async Task Handle_WithValidCommand_ShouldCreate{Entity}()
    {
        // Arrange
        var command = new Create{Entity}Command(...);
        
        // Act
        var result = await _handler.HandleAsync(command, CancellationToken.None);
        
        // Assert
        result.IsSuccess.Should().BeTrue();
        await _repository.Received(1).AddAsync(Arg.Any<{Entity}>(), Arg.Any<CancellationToken>());
        await _repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
```

### Integration Test Pattern

```csharp
public class {Entity}IntegrationTests : IClassFixture<WallowApiFactory>, IAsyncLifetime
{
    private readonly WallowApiFactory _factory;
    private readonly HttpClient _client;
    
    public {Entity}IntegrationTests(WallowApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }
    
    public async Task InitializeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<{ModuleName}DbContext>();
        await context.Database.EnsureCreatedAsync();
    }
    
    [Fact]
    public async Task Create{Entity}_ShouldReturn201()
    {
        // Arrange
        var request = new Create{Entity}Request { ... };
        
        // Act
        var response = await _client.PostAsJsonAsync("/api/{modulename}/{entities}", request);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }
}
```

## Background Jobs

Implement `IRecurringJobRegistration` from Shared.Kernel:

```csharp
public class {ModuleName}RecurringJobs : IRecurringJobRegistration
{
    public void RegisterJobs(IRecurringJobManager manager)
    {
        manager.AddOrUpdate<{SomeJob}>(
            "{modulename}-job-id",
            job => job.ExecuteAsync(CancellationToken.None),
            Cron.Daily());
    }
}

// Register in Infrastructure extensions
services.AddSingleton<IRecurringJobRegistration, {ModuleName}RecurringJobs>();
```

## Common Patterns Checklist

- [ ] Schema name is lowercase, no underscores
- [ ] Use `StronglyTypedIdConverter<T>` for ID properties
- [ ] All ITenantScoped entities have tenant query filters
- [ ] Column names use snake_case (created_at, tenant_id)
- [ ] Indexes on TenantId for all tenant-scoped tables
- [ ] Domain events extend `DomainEvent` base class
- [ ] Integration events extend `IntegrationEvent` base class
- [ ] Integration events include TenantId property
- [ ] Event handlers are static methods with `HandleAsync` name
- [ ] No direct references to other module assemblies
- [ ] Tests follow Arrange-Act-Assert pattern
- [ ] Test names: `Method_Condition_ExpectedResult`
