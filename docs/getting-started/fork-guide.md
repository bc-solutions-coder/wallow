# Fork Guide

How to fork Wallow, configure modules, add new functionality, and stay in sync with upstream changes.

---

## Overview

Wallow is designed as a base platform that teams fork and extend. Each fork becomes an independent product while retaining the ability to pull improvements from the upstream Wallow repository.

```
wallow (upstream)          your-product (fork)
    |                            |
    |-- main <----- PR -------- feature-branches
    |                            |
    |   generic improvements     |   product-specific code
    |   flow back via PR         |   lives only in fork
    |                            |
    v2.0 ---- git merge ------> fork pulls upstream
    v2.1 ---- git merge ------> fork pulls upstream
```

---

## Prerequisites

- .NET 10 SDK
- Docker and Docker Compose
- PostgreSQL (via Docker or standalone)
- Git

---

## Trademark and Attribution

See the `NOTICE` file in the repository root. "Wallow" is a trademark of BC Solutions Ltd. If you distribute a derivative product under a different name, you must:

- Remove or replace the Wallow name and branding (see Approach B below)
- Retain the `NOTICE` file and MIT license attribution
- Not use "Wallow" in your product name without written permission

---

## Approach A (Recommended): Keep Namespaces, Customize via Config

The simplest fork strategy is to **keep all `Wallow.*` namespaces unchanged** and customize the user-facing product identity through configuration only:

1. **Fork and clone** the repository
2. **Edit `branding.json`** in the repo root to set your product name, icon, tagline, and theme colors
3. **Edit `appsettings.json`** to configure CORS origins, connection strings, SMTP sender name, and OpenTelemetry service name
4. **Set up the merge driver** so upstream merges don't overwrite your config (see "Merge Driver Setup" below)

This approach has **zero risk of silent failures** and gives you the easiest upstream sync path. All user-facing text (page titles, login screens, emails) is driven by `BrandingOptions` which reads from `branding.json`.

### Merge Driver Setup

The repository ships a `.gitattributes` that marks fork-owned files with `merge=ours`. Activate the merge driver:

```bash
git config merge.ours.driver true
```

This ensures that `appsettings*.json`, `branding.json`, `docker/.env`, `CLAUDE.md`, and `.claude/**` keep your fork's version during upstream merges.

---

## Approach B (Advanced): Full Namespace Rename

If you need to remove all `Wallow` references from source code (e.g., for white-label distribution), follow this approach. **Be aware that namespace renaming can cause silent failures** — see the checklist below.

### 1. Fork and clone

```bash
git clone git@github.com:your-org/YourProduct.git
cd YourProduct
```

### 2. Rename the solution file

```bash
mv Wallow.slnx YourProduct.slnx
```

### 3. Rename namespaces across the codebase

Every `Wallow.*` namespace, project name, and assembly reference must become `YourProduct.*`.

**Rename directories and project files:**

```bash
# Rename project directories (src and tests)
find src tests -type d -name 'Wallow.*' | while read dir; do
  mv "$dir" "$(echo "$dir" | sed 's/Wallow\./YourProduct./')"
done

# Rename .csproj files
find src tests -name 'Wallow.*.csproj' | while read f; do
  mv "$f" "$(echo "$f" | sed 's/Wallow\./YourProduct./')"
done
```

**Replace namespace strings in all source files:**

```bash
find . \( -name '*.sln' -o -name '*.csproj' -o -name '*.cs' -o -name '*.json' \
       -o -name 'Dockerfile' -o -name '*.yml' -o -name '*.yaml' \) \
  -not -path '*/bin/*' -not -path '*/obj/*' -not -path '*/.git/*' \
  -exec sed -i '' 's/Wallow\./YourProduct./g' {} +

# Catch standalone "Wallow" references (log messages, display names, etc.)
# Review these manually — some may be intentional:
find . \( -name '*.cs' -o -name '*.json' -o -name '*.yml' \) \
  -not -path '*/bin/*' -not -path '*/obj/*' -not -path '*/.git/*' \
  -exec grep -l '"Wallow"' {} +
```

Alternatively, use your IDE's global Find and Replace. JetBrains Rider handles this well with **Edit > Find and Replace in Files**.

### 4. Update the solution file references

Open `YourProduct.slnx` and verify all project paths point to the renamed `.csproj` files. The `sed` pass above should handle this, but confirm with:

```bash
grep 'Wallow\.' YourProduct.slnx
```

Should return nothing.

### 5. Update configuration files

| File | What to change |
|------|---------------|
| `docker/.env` | `COMPOSE_PROJECT_NAME` |
| `docker/docker-compose.yml` | Network name, container prefixes |
| `appsettings.json` | Application name and branding |
| `branding.json` | `appName`, `tagline`, icon |
| `Dockerfile` | Solution file and DLL references |
| `.github/workflows/*.yml` | Database names, connection strings, deploy paths, image names |

### 6. Update Wolverine assembly scanning

In `Program.cs`, update the assembly prefix filter to match your new namespace:

```csharp
foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies()
    .Where(a => a.GetName().Name?.StartsWith("YourProduct.") == true))
{
    opts.Discovery.IncludeAssembly(assembly);
}
```

### 7. Build and verify

```bash
dotnet restore YourProduct.slnx
dotnet build YourProduct.slnx
./scripts/run-tests.sh
```

Fix any remaining `Wallow` references the compiler surfaces. Also verify the Dockerfile:

```bash
grep -i wallow Dockerfile
```

Should return nothing.

### Silent Failure Checklist

These components reference `"Wallow"` as a string literal and will **fail silently** if you rename namespaces but miss them:

| Component | File | What breaks |
|-----------|------|-------------|
| **ModuleEnricher** | `src/Wallow.Api/Logging/ModuleEnricher.cs` | Log enrichment stops tagging module names |
| **OpenTelemetry ServiceName** | `appsettings*.json` → `OpenTelemetry:ServiceName` | Traces/metrics report wrong service name |
| **Diagnostics ActivitySource** | `src/Shared/Wallow.Shared.Kernel/Diagnostics.cs` | Custom traces stop appearing (`new ActivitySource("Wallow")`) |
| **SMTP DefaultFromName** | `appsettings.json` → `Smtp:DefaultFromName` | Emails show "Wallow" as sender |
| **branding.json** | `branding.json` → `appName` | Auth pages show "Wallow" in titles |
| **Email templates** | Razor templates in `Wallow.Auth` | Email bodies may contain hardcoded product name |

After renaming, search for remaining literal references:

```bash
grep -r '"Wallow"' --include='*.cs' --include='*.json' --include='*.razor' \
  --exclude-dir=bin --exclude-dir=obj .
```

---

## CORS Cleanup

After forking, remove any upstream origins from the CORS configuration. Edit `appsettings.json`:

```json
{
  "Cors": {
    "AllowedOrigins": ["https://your-app.example.com"]
  }
}
```

Only include origins that your fork actually serves.

---

## Data Protection (GDPR)

If you operate a fork that processes personal data of EU residents, you are the **data controller**. Key responsibilities:

- Update the privacy policy (`/privacy` page) to reflect your organization
- Configure data retention policies appropriate for your jurisdiction
- Ensure the `NOTICE` file attribution does not imply upstream Wallow is the data processor
- Review tenant data isolation — each module uses separate PostgreSQL schemas with query filters on `TenantId`

---

## Configuring Modules

Wallow ships with multiple modules. Most are enabled by default and can be toggled via feature flags -- no source code changes required. Identity is always registered (not behind a feature flag).

### Enabling and disabling modules

Modules are controlled by the `FeatureManagement` section in `appsettings.json`. Each key maps to `Modules.{ModuleName}` with a boolean value:

```json
{
  "FeatureManagement": {
    "Modules.Billing": true,
    "Modules.Branding": true,
    "Modules.Storage": true,
    "Modules.Notifications": true,
    "Modules.Messaging": true,
    "Modules.Announcements": true,
    "Modules.Configuration": true,
    "Modules.Inquiries": true,
    "Modules.ApiKeys": false
  }
}
```

To disable a module, set its value to `false`:

```json
{
  "FeatureManagement": {
    "Modules.Billing": false
  }
}
```

This is wired in `WallowModules.cs`, which uses `IFeatureManager` to check feature flags before registering each module. Identity is always registered as a required platform dependency. When a module is disabled, its DI services, database migrations, API controllers, and Wolverine handlers are all excluded from the application.

### Module-specific configuration

Each module reads its own configuration section from `appsettings.json`. See the [Configuration Guide](configuration.md) for the full reference of all configuration sections (`Smtp`, `Storage`, `OpenTelemetry`, etc.).

### Environment-specific overrides

Use `appsettings.{Environment}.json` or environment variables to configure modules per deployment target:

```bash
# Disable billing in development
FeatureManagement__Modules.Billing=false

# Configure SMTP for production
Smtp__Host=smtp.example.com
Smtp__Port=587
Smtp__UseSsl=true
```

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

### 5. Register the module

Add to `WallowModules.cs`:

```csharp
if (await featureManager.IsEnabledAsync("Modules.YourModule"))
    services.AddYourModuleModule(configuration);
```

Add to initialization in `InitializeWallowModulesAsync()`:

```csharp
await app.InitializeYourModuleModuleAsync();
```

### 6. Add to the solution file

```bash
dotnet sln YourProduct.slnx add src/Modules/YourModule/YourProduct.YourModule.Domain
dotnet sln YourProduct.slnx add src/Modules/YourModule/YourProduct.YourModule.Application
dotnet sln YourProduct.slnx add src/Modules/YourModule/YourProduct.YourModule.Infrastructure
dotnet sln YourProduct.slnx add src/Modules/YourModule/YourProduct.YourModule.Api
```

### 7. Handler discovery (automatic)

Wolverine scans all assemblies whose names start with `YourProduct.` (after renaming from `Wallow`) and uses in-memory transport for all messaging. No manual routing configuration is needed. Just create handlers following Wolverine conventions:

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

For more detail, see the [Module Creation Guide](../architecture/module-creation.md).

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
        Result<OrderDto> result = await _bus.InvokeAsync<Result<OrderDto>>(
            new CreateOrderCommand(request.CustomerId, request.Items));
        return result.ToActionResult();
    }

    [HttpGet("{id:guid}")]
    [HasPermission(PermissionType.OrdersRead)]
    public async Task<IActionResult> GetById(Guid id)
    {
        Result<OrderDto> result = await _bus.InvokeAsync<Result<OrderDto>>(
            new GetOrderByIdQuery(id));
        return result.ToActionResult();
    }
}
```

### 2. Add permissions

If your module needs new permissions, add string constants to `PermissionType` in `src/Shared/Wallow.Shared.Kernel/Identity/Authorization/PermissionType.cs` and update the role-to-permission mapping in the Identity module's `RolePermissionMapping.cs`.

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
// In Notifications.Application/EventHandlers/
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

Wolverine automatically discovers handlers by convention. No manual registration is needed.

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

---

## Adding Plugins and Extensions

Wallow includes a plugin system for product-specific extensions that load dynamically without modifying core code. Plugins are the recommended way to add fork-specific functionality because they don't create merge conflicts when syncing upstream.

### Plugin structure

A plugin is a .NET class library that implements `IWallowPlugin` and ships with a `plugin.json` manifest:

```
plugins/
  your-plugin/
    plugin.json
    YourPlugin.dll
```

**Manifest (`plugin.json`):**

```json
{
  "id": "your-plugin",
  "name": "Your Plugin",
  "version": "1.0.0",
  "description": "Product-specific extension",
  "author": "Your Team",
  "minWallowVersion": "0.2.0",
  "entryAssembly": "YourPlugin.dll",
  "dependencies": [],
  "requiredPermissions": ["storage:read", "messaging:send"],
  "exportedServices": []
}
```

**Plugin entry point:**

```csharp
public class YourPlugin : IWallowPlugin
{
    public PluginManifest Manifest => // loaded from plugin.json

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        // Register your DI services
    }

    public Task InitializeAsync(PluginContext context)
    {
        // Run startup logic
        return Task.CompletedTask;
    }

    public Task ShutdownAsync()
    {
        // Cleanup
        return Task.CompletedTask;
    }
}
```

### Plugin configuration

```json
{
  "Plugins": {
    "PluginsDirectory": "plugins/",
    "AutoDiscover": true,
    "AutoEnable": false,
    "Permissions": {
      "your-plugin": ["storage:read", "messaging:send"]
    }
  }
}
```

| Setting | Default | Description |
|---------|---------|-------------|
| `PluginsDirectory` | `plugins/` | Directory to scan for plugin assemblies |
| `AutoDiscover` | `true` | Automatically discover plugins on startup |
| `AutoEnable` | `false` | Automatically load all discovered plugins |
| `Permissions` | `{}` | Per-plugin permission grants |

Plugins are loaded in an isolated `AssemblyLoadContext`, so they cannot interfere with core module assemblies.

### When to use plugins vs modules

| Use case | Approach |
|----------|----------|
| Generic capability useful across products | Module in core Wallow |
| Product-specific feature that only your fork needs | Plugin |
| Feature you want to develop in your fork and later contribute upstream | Start as a plugin, then convert to a module when contributing |

---

## Running Tests for New Modules

### 1. Create the test project

Each module uses a single test project with subdirectories for each layer:

```
tests/Modules/YourModule/YourProduct.YourModule.Tests/
  Domain/
  Application/
  Infrastructure/
```

```bash
mkdir -p tests/Modules/YourModule
cd tests/Modules/YourModule
dotnet new xunit -n YourProduct.YourModule.Tests
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
dotnet sln YourProduct.slnx add tests/Modules/YourModule/YourProduct.YourModule.Tests
```

### 2. Unit tests

Test handlers in isolation by mocking repositories and services:

```csharp
[Fact]
public async Task Should_create_order()
{
    IOrderRepository repo = Substitute.For<IOrderRepository>();
    CreateOrderCommand command = new(customerId, items);

    Result<OrderDto> result = await CreateOrderHandler.HandleAsync(command, repo, CancellationToken.None);

    result.IsSuccess.Should().BeTrue();
    await repo.Received(1).SaveChangesAsync();
}
```

### 3. Integration tests

Use the shared `WebApplicationFactory` with Testcontainers from `Tests.Common`. Prefer `ICollectionFixture` over `IClassFixture` for container sharing:

```csharp
[Collection("Api")]
public class OrdersControllerTests
{
    private readonly HttpClient _client;

    public OrdersControllerTests(WallowApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreateOrder_returns_201()
    {
        HttpResponseMessage response = await _client.PostAsJsonAsync("/api/orders", request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }
}
```

Integration tests require Docker. Testcontainers spins up ephemeral Postgres and Valkey containers.

### 4. Run tests

```bash
# All tests
./scripts/run-tests.sh

# Only your module
./scripts/run-tests.sh tests/Modules/YourModule/YourProduct.YourModule.Tests
```

---

## Syncing Upstream Changes

### Initial setup (one-time)

```bash
git remote add upstream https://github.com/your-org/Wallow.git
git fetch upstream
```

### Pulling updates (tagged-release sync)

For stability, sync from tagged releases rather than `upstream/main`:

```bash
git fetch upstream --tags
git checkout main
git merge v2.1.0    # merge a specific release tag
```

Alternatively, track the latest main:

```bash
git fetch upstream
git checkout main
git merge upstream/main
```

### Resolving conflicts

Conflicts typically occur in files where you renamed `Wallow` to `YourProduct`. The recommended workflow:

1. Accept the upstream version of the conflicted file
2. Re-apply the `Wallow -> YourProduct` replacement on that file
3. Review the diff to confirm the upstream logic change was preserved

For large upstream merges, consider cherry-picking specific commits:

```bash
git cherry-pick <commit-hash>
```

### Reducing merge friction

- **Avoid modifying shared projects** -- `Shared.Kernel` and `Shared.Contracts` are the highest-conflict areas. Extend them sparingly.
- **Keep product-specific logic in plugins or your own modules** -- not in core projects.
- **Merge upstream regularly** -- small, frequent merges are easier than large catch-up merges.
- **Prefer extending over modifying** -- when adding features to existing modules, add new files rather than editing existing ones where possible.
- **Track upstream-intended commits** -- prefix commits meant for contribution with `[wallow]` in the commit message for easy identification.

### Recommended sync cadence

| Stage | Cadence |
|-------|---------|
| Active upstream development | Weekly merge |
| Stable upstream, active fork development | Bi-weekly merge |
| Both stable | Monthly merge or on release tags |

---

## Contributing Changes Back Upstream

When you build something generic in your fork that would benefit the base platform, contribute it back via pull request.

### Workflow

1. **Build the feature in your fork** -- develop and validate it in your product context.
2. **Identify generic vs product-specific parts** -- separate business logic that is product-specific from infrastructure that is reusable.
3. **Re-implement generically in a clean branch off upstream/main:**

```bash
git fetch upstream
git checkout -b feat/my-feature upstream/main
# Implement the generic version
git push origin feat/my-feature
```

4. **Open a PR against the upstream repository** following Wallow's commit conventions (`feat:`, `fix:`, etc.).
5. **After the PR is merged**, sync upstream into your fork to replace your fork-specific version with the upstream one:

```bash
git fetch upstream
git checkout main
git merge upstream/main
git push origin main
```

### Guidelines for upstream contributions

- Remove all product-specific references, naming, and configuration.
- Follow the existing module patterns: Clean Architecture layers, strongly-typed IDs, Result pattern.
- Include tests matching the upstream coverage standards (90% minimum).
- Integration events go in `Shared.Contracts`. Domain logic stays within the module.
- Update documentation in `docs/` if adding a new module or significant feature.

---

## Checklist (Approach A)

- [ ] Fork created and cloned
- [ ] `branding.json` customized with your product identity
- [ ] `appsettings.json` configured (CORS, SMTP, OpenTelemetry service name)
- [ ] Merge driver activated (`git config merge.ours.driver true`)
- [ ] `dotnet build` succeeds
- [ ] `./scripts/run-tests.sh` passes
- [ ] Upstream remote added for future syncing
- [ ] Module toggles configured in `FeatureManagement` section

## Checklist (Approach B -- Full Rename)

- [ ] All `Wallow.*` references renamed to `YourProduct.*`
- [ ] Solution file renamed and project paths updated
- [ ] Docker Compose configuration updated
- [ ] CI/CD workflows updated
- [ ] Dockerfile updated
- [ ] Wolverine assembly scanning prefix updated
- [ ] `dotnet build` succeeds
- [ ] `./scripts/run-tests.sh` passes
