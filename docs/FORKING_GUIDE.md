# Forking Guide

How to create a new product from the Foundry template.

## Prerequisites

- .NET 10 SDK
- Docker and Docker Compose
- PostgreSQL 18 (via Docker or standalone)
- Git

## Step-by-Step: Fork and Rename

### 1. Fork and clone

```bash
# Fork via GitHub UI, then:
git clone git@github.com:your-org/YourProduct.git
cd YourProduct
```

### 2. Rename the solution file

```bash
mv Foundry.slnx YourProduct.slnx
```

### 3. Rename namespaces across the codebase

Every `Foundry.*` namespace, project name, and assembly reference must become `YourProduct.*`.

**Files to rename (directories and .csproj files):**

```bash
# Rename project directories
find src -type d -name 'Foundry.*' | while read dir; do
  mv "$dir" "$(echo "$dir" | sed 's/Foundry\./YourProduct./')"
done

# Rename .csproj files inside those directories
find src -name 'Foundry.*.csproj' | while read f; do
  mv "$f" "$(echo "$f" | sed 's/Foundry\./YourProduct./')"
done

# Same for test projects
find tests -type d -name 'Foundry.*' | while read dir; do
  mv "$dir" "$(echo "$dir" | sed 's/Foundry\./YourProduct./')"
done
find tests -name 'Foundry.*.csproj' | while read f; do
  mv "$f" "$(echo "$f" | sed 's/Foundry\./YourProduct./')"
done
```

**Replace namespace strings in all source files:**

```bash
# Solution file, all .csproj, all .cs, all .json config, Dockerfile, CI workflows
find . \( -name '*.sln' -o -name '*.csproj' -o -name '*.cs' -o -name '*.json' \
       -o -name 'Dockerfile' -o -name '*.yml' -o -name '*.yaml' \) \
  -not -path '*/bin/*' -not -path '*/obj/*' -not -path '*/.git/*' \
  -exec sed -i '' 's/Foundry\./YourProduct./g' {} +

# Catch standalone "Foundry" references (log messages, display names, etc.)
# Review these manually — some may be intentional:
find . \( -name '*.cs' -o -name '*.json' -o -name '*.yml' \) \
  -not -path '*/bin/*' -not -path '*/obj/*' -not -path '*/.git/*' \
  -exec grep -l '"Foundry"' {} +
```

Alternatively, use your IDE's global rename/refactor. JetBrains Rider handles this well with **Edit > Find and Replace in Files**.

### 4. Update the solution file references

Open `YourProduct.sln` and verify all project paths point to the renamed `.csproj` files. The `sed` pass above should handle this, but confirm with:

```bash
grep 'Foundry\.' YourProduct.sln
```

Should return nothing.

### 5. Update Docker Compose

In `docker/docker-compose.yml` and the environment-specific overrides:

- Change the `foundry` network name to `yourproduct`
- Update `COMPOSE_PROJECT_NAME` in `docker/.env` (and `.env.example`)
- Update container name prefixes if desired (currently `${COMPOSE_PROJECT_NAME:-foundry}-*`)

### 6. Update Keycloak realm

In `src/Foundry.Api/appsettings.json` (and per-environment overrides):

```json
{
  "Keycloak": {
    "realm": "yourproduct",
    "resource": "yourproduct-api"
  },
  "KeycloakAdmin": {
    "realm": "yourproduct",
    "resource": "yourproduct-api"
  }
}
```

Also update the realm export in `docker/keycloak/realm-export.json` if present, or create a new realm in the Keycloak admin console.

### 7. Update CI/CD workflows

In `.github/workflows/`:

- `ci.yml` -- update database name in `POSTGRES_DB` and connection strings
- `deploy-dev.yml`, `deploy-staging.yml`, `deploy-prod.yml` -- update deploy script paths (currently `/opt/foundry/scripts/deploy.sh`)
- Update `IMAGE_NAME` if you changed the GitHub repository name (it defaults to `${{ github.repository }}`)

### 8. Update Dockerfile

The `Dockerfile` references `Foundry.slnx` and `Foundry.Api.dll`. After the sed pass, verify:

```bash
grep -i foundry Dockerfile
```

Should return nothing.

### 9. Build and verify

```bash
dotnet restore YourProduct.sln
dotnet build YourProduct.sln
dotnet test
```

Fix any remaining `Foundry` references the compiler surfaces.

---

## Adding a New Module

### 1. Create the module directory structure

```
src/Modules/YourModule/
  YourProduct.YourModule.Domain/
  YourProduct.YourModule.Application/
  YourProduct.YourModule.Infrastructure/
  YourProduct.YourModule.Api/
```

### 2. Create the four projects

```bash
cd src/Modules/YourModule

dotnet new classlib -n YourProduct.YourModule.Domain
dotnet new classlib -n YourProduct.YourModule.Application
dotnet new classlib -n YourProduct.YourModule.Infrastructure
dotnet new classlib -n YourProduct.YourModule.Api
```

### 3. Wire up project references (Clean Architecture)

```bash
# Application depends on Domain
dotnet add YourProduct.YourModule.Application reference YourProduct.YourModule.Domain

# Infrastructure depends on Application (and transitively Domain)
dotnet add YourProduct.YourModule.Infrastructure reference YourProduct.YourModule.Application

# Api depends on Application
dotnet add YourProduct.YourModule.Api reference YourProduct.YourModule.Application

# Infrastructure also needs Shared.Kernel for base classes
dotnet add YourProduct.YourModule.Infrastructure reference ../../Shared/YourProduct.Shared.Kernel

# Api needs Infrastructure for DI registration
dotnet add YourProduct.YourModule.Api reference YourProduct.YourModule.Infrastructure
```

Domain has **no** project references.

### 4. Add shared events

Create integration event records in:

```
src/Shared/YourProduct.Shared.Contracts/YourModule/Events/
```

Example:

```csharp
namespace YourProduct.Shared.Contracts.YourModule.Events;

public sealed record SomethingHappenedEvent : IntegrationEvent
{
    public required Guid SomethingId { get; init; }
    public required string Name { get; init; }
}
```

The `IntegrationEvent` base record provides `EventId` and `OccurredAt` automatically.

### 5. Register the module in FoundryModules.cs

Add a using directive at the top:

```csharp
using YourProduct.YourModule.Infrastructure.Extensions;
```

Add to service registration in `AddFoundryModules()`:

```csharp
services.AddYourModuleModule(configuration);
```

Add to initialization in `InitializeFoundryModulesAsync()`:

```csharp
await app.InitializeYourModuleModuleAsync();
```

### 6. Add to the solution file

```bash
dotnet sln YourProduct.sln add src/Modules/YourModule/YourProduct.YourModule.Domain
dotnet sln YourProduct.sln add src/Modules/YourModule/YourProduct.YourModule.Application
dotnet sln YourProduct.sln add src/Modules/YourModule/YourProduct.YourModule.Infrastructure
dotnet sln YourProduct.sln add src/Modules/YourModule/YourProduct.YourModule.Api
```

### 7. RabbitMQ and handler discovery (automatic)

Wolverine is configured with `UseConventionalRouting()` which automatically creates exchanges and queues based on message types. No manual routing configuration is needed.

Handler discovery is also automatic -- Wolverine scans all assemblies whose names start with `Foundry.` (or `YourProduct.` after renaming). Just create handlers following Wolverine conventions:

```csharp
public static class CreateSomethingHandler
{
    public static async Task<Result<SomethingDto>> HandleAsync(
        CreateSomethingCommand command,
        ISomethingRepository repo,
        CancellationToken ct)
    {
        // Implementation
    }
}
```

No manual assembly registration is required.

**Note:** After renaming from `Foundry` to `YourProduct`, update the assembly scanning in `Program.cs`:

```csharp
foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()
    .Where(a => a.GetName().Name?.StartsWith("YourProduct.") == true))
{
    opts.Discovery.IncludeAssembly(assembly);
}
```

---

## Configuring Multi-Tenancy for New Modules

Every module that stores tenant-specific data must integrate with the multi-tenancy infrastructure from `Shared.Kernel`.

### 1. Mark domain entities as tenant-scoped

Implement `ITenantScoped` on any entity that belongs to a tenant:

```csharp
using YourProduct.Shared.Kernel.MultiTenancy;

public class Order : AggregateRoot, ITenantScoped
{
    public string OrderNumber { get; private set; }
    public TenantId TenantId { get; set; }
}
```

### 2. Apply global query filters in your DbContext

In your module's `DbContext.OnModelCreating`, add a query filter for each tenant-scoped entity:

```csharp
public sealed class OrderDbContext : DbContext
{
    private readonly ITenantContext _tenantContext;

    public OrderDbContext(
        DbContextOptions<OrderDbContext> options,
        ITenantContext tenantContext) : base(options)
    {
        _tenantContext = tenantContext;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("orders");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OrderDbContext).Assembly);

        modelBuilder.Entity<Order>()
            .HasQueryFilter(e => e.TenantId == _tenantContext.TenantId);
    }
}
```

### 3. Register the TenantSaveChangesInterceptor

In your module's service registration, add the interceptor so `TenantId` is automatically stamped on new entities:

```csharp
services.AddDbContext<OrderDbContext>((sp, options) =>
{
    options.UseNpgsql(connectionString);
    options.AddInterceptors(sp.GetRequiredService<TenantSaveChangesInterceptor>());
});
```

### 4. Create a DesignTimeTenantContext

EF Core tooling needs an `ITenantContext` at migration time. Add this to your Infrastructure project's `Persistence` folder:

```csharp
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

### 5. Dapper queries

When using Dapper for reads, you must filter by tenant manually:

```sql
WHERE tenant_id = @TenantId
```

Pass `_tenantContext.TenantId.Value` as the parameter.

---

## Adding API Endpoints

Controllers live in the `Api` layer of your module and depend only on `Application`.

### 1. Create a controller

In `YourProduct.YourModule.Api/Controllers/`:

```csharp
[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly IMessageBus _bus;

    public OrdersController(IMessageBus bus)
    {
        _bus = bus;
    }

    [HttpPost]
    [HasPermission(PermissionType.OrdersCreate)]
    public async Task<IActionResult> Create([FromBody] CreateOrderRequest request)
    {
        var result = await _bus.InvokeAsync<Result<OrderDto>>(
            new CreateOrderCommand(request.CustomerId, request.Items));
        return result.ToActionResult();
    }

    [HttpGet("{id:guid}")]
    [HasPermission(PermissionType.OrdersRead)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _bus.InvokeAsync<Result<OrderDto>>(
            new GetOrderByIdQuery(id));
        return result.ToActionResult();
    }
}
```

### 2. Add permissions

If your module needs new permissions, add entries to the `PermissionType` enum in `YourProduct.Identity.Domain/Enums/PermissionType.cs` and update the role-to-permission mapping in `YourProduct.Identity.Infrastructure/Authorization/RolePermissionMapping.cs`.

### 3. Request/Response contracts

Define request and response types in the Api layer:

```csharp
public record CreateOrderRequest(Guid CustomerId, List<OrderItemRequest> Items);
public record OrderItemRequest(Guid ProductId, int Quantity);
```

DTOs live in the Application layer. Requests and responses live in the Api layer.

---

## Adding Domain Events and Consumers

### Define the event

Add integration events to `Shared.Contracts` so any module can consume them:

```
src/Shared/YourProduct.Shared.Contracts/YourModule/Events/OrderPlacedEvent.cs
```

```csharp
namespace YourProduct.Shared.Contracts.YourModule.Events;

public sealed record OrderPlacedEvent : IntegrationEvent
{
    public required Guid OrderId { get; init; }
    public required Guid CustomerId { get; init; }
    public required decimal Total { get; init; }
}
```

Events use primitive types only -- no strongly-typed domain IDs. This keeps serialization simple across module boundaries. Name events in past tense. They are facts, not commands.

### Publish the event

From any handler, publish after the operation succeeds:

```csharp
await bus.PublishAsync(new OrderPlacedEvent
{
    OrderId = order.Id,
    CustomerId = order.CustomerId,
    Total = order.Total
});
```

### Create a consumer in another module

In the consuming module's Application or Infrastructure layer, Wolverine discovers handlers by convention:

```csharp
// In Communications.Application/Channels/InApp/EventHandlers/
public static class OrderPlacedEventHandler
{
    public static async Task HandleAsync(
        OrderPlacedEvent @event,
        INotificationService notifications,
        CancellationToken ct)
    {
        await notifications.CreateAsync(
            @event.CustomerId,
            $"Order {@event.OrderId} placed for {@event.Total:C}",
            ct);
    }
}
```

Wolverine's `UseConventionalRouting()` automatically creates the necessary RabbitMQ exchanges and queues. No manual registration is needed.

---

## Adding Migrations

Each module manages its own migrations through its Infrastructure project.

### Create a migration

```bash
dotnet ef migrations add InitialCreate \
    --project src/Modules/YourModule/YourProduct.YourModule.Infrastructure \
    --startup-project src/YourProduct.Api \
    --context YourModuleDbContext
```

### Apply manually (optional)

```bash
dotnet ef database update \
    --project src/Modules/YourModule/YourProduct.YourModule.Infrastructure \
    --startup-project src/YourProduct.Api \
    --context YourModuleDbContext
```

Migrations also run automatically at startup via `InitializeYourModuleModuleAsync()`.

### Subsequent migrations

For schema changes after initial creation, follow the same pattern with a descriptive name:

```bash
dotnet ef migrations add AddShippingAddress \
    --project src/Modules/YourModule/YourProduct.YourModule.Infrastructure \
    --startup-project src/YourProduct.Api \
    --context YourModuleDbContext
```

---

## Running Tests for New Modules

### 1. Create the test project

The codebase uses different naming conventions for test projects depending on scope:
- `YourModule.Domain.Tests` - Unit tests for domain layer
- `YourModule.Application.Tests` - Unit tests for application layer
- `Modules.YourModule.Tests` or `YourProduct.YourModule.Tests` - Integration tests

Example for domain tests:

```bash
mkdir -p tests/Modules/YourModule/YourModule.Domain.Tests
cd tests/Modules/YourModule/YourModule.Domain.Tests
dotnet new xunit -n YourModule.Domain.Tests
```

Add references to the module layers and the shared test infrastructure:

```bash
dotnet add reference ../../../src/Modules/YourModule/YourProduct.YourModule.Domain
dotnet add reference ../../../src/Modules/YourModule/YourProduct.YourModule.Application
dotnet add reference ../../../src/Modules/YourModule/YourProduct.YourModule.Infrastructure
dotnet add reference ../../YourProduct.Tests.Common/YourProduct.Tests.Common.csproj
```

Add the test project to the solution:

```bash
dotnet sln YourProduct.sln add tests/Modules/YourModule/Modules.YourModule.Tests
```

### 2. Unit tests

Test handlers in isolation by mocking repositories and services:

```csharp
[Fact]
public async Task Should_create_order()
{
    var repo = Substitute.For<IOrderRepository>();
    var command = new CreateOrderCommand(customerId, items);

    var result = await CreateOrderHandler.HandleAsync(command, repo, CancellationToken.None);

    result.IsSuccess.Should().BeTrue();
    await repo.Received(1).SaveChangesAsync();
}
```

### 3. Integration tests

Use the shared `WebApplicationFactory` with Testcontainers from `Tests.Common`:

```csharp
public class OrdersControllerTests : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client;

    public OrdersControllerTests(ApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreateOrder_returns_201()
    {
        var response = await _client.PostAsJsonAsync("/api/orders", request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }
}
```

Integration tests require Docker. Testcontainers spins up ephemeral Postgres, RabbitMQ, and Redis containers.

### 4. Run tests

```bash
# All tests
dotnet test

# Only your module
dotnet test tests/Modules/YourModule/Modules.YourModule.Tests

# By category
dotnet test --filter "Category=Unit"
dotnet test --filter "Category=Integration"
```

---

## Pulling Upstream Changes

Keep your fork connected to the Foundry base to receive improvements.

### Initial setup (once)

```bash
git remote add upstream https://github.com/your-org/Foundry.git
git fetch upstream
```

### Pull updates

```bash
git fetch upstream
git checkout main
git merge upstream/main
```

Conflicts will arise in files where you renamed `Foundry` to `YourProduct`. Resolve by accepting your rename and applying the upstream logic change. A typical workflow:

1. Accept the upstream version of the conflicted file
2. Re-apply the `Foundry -> YourProduct` sed replacement on that file
3. Review the diff to confirm the logic change was preserved

For large upstream merges, consider cherry-picking specific commits instead:

```bash
git cherry-pick <commit-hash>
```

### Reducing merge friction

- Avoid modifying `Shared.Kernel` and `Shared.Contracts` unless necessary -- these are the highest-conflict areas
- Keep product-specific logic in your own modules, not in the shared/core projects
- Periodically rebase your customization branch to stay close to upstream
