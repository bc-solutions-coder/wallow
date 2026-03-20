# Module Registration Flow - Wallow Modular Monolith

## Overview

Wallow uses **automatic module discovery** via the `IModuleRegistration` interface. Modules are discovered at startup by scanning for `Wallow.*.Api.dll` assemblies that contain types implementing `IModuleRegistration`.

This replaced the manual extension method pattern where each module was explicitly registered in `Program.cs`.

## Auto-Discovery Architecture

### IModuleRegistration Interface

Located in `src/Shared/Wallow.Shared.Kernel/Modules/IModuleRegistration.cs`:

```csharp
public interface IModuleRegistration
{
    static abstract string ModuleName { get; }
    static abstract void ConfigureServices(IServiceCollection services, IConfiguration configuration);
    static abstract Task InitializeAsync(WebApplication app);
    static abstract Assembly? HandlerAssembly { get; }
    static abstract void ConfigureMessaging(WolverineOptions options);
}
```

### Discovery Mechanism

Located in `src/Shared/Wallow.Shared.Infrastructure/Modules/ModuleDiscovery.cs`:

**Three-Phase Auto-Discovery**:

1. **Service Registration Phase**: `AddWallowModules(configuration)` scans assemblies and calls `ConfigureServices()` on each discovered module
2. **Wolverine Configuration Phase**: `ConfigureWallowModules(services)` discovers handler assemblies and calls `ConfigureMessaging()` on each module
3. **Initialization Phase**: `UseWallowModulesAsync()` calls `InitializeAsync()` on each module for migrations and seeding

**Discovery Logic**:
- Scans all loaded assemblies for `Wallow.*.Api.dll` pattern
- Finds types implementing `IModuleRegistration`
- Filters to concrete, non-abstract types
- Returns discovered types in assembly order

### Program.cs (Simplified)

The new simplified `Program.cs`:

```csharp
// 1. Service registration
builder.Services.AddWallowModules(builder.Configuration);

// 2. Wolverine configuration
builder.Host.UseWolverine(opts =>
{
    opts.Services.AddResourceMessaging();

    opts.UseRabbitMq(rabbitMQ => {
        rabbitMQ.HostName = rabbitConfig.Host;
        rabbitMQ.Port = rabbitConfig.Port;
    })
    .AutoProvision()
    .AutoPurgeOnStartup();
    
    // Auto-discover all modules
    opts.ConfigureWallowModules(builder.Services);
});

var app = builder.Build();

// 3. Module initialization (migrations, seeders)
await app.UseWallowModulesAsync();

app.Run();
```

**What's Eliminated**:
- No manual `builder.Services.Add{Module}Module()` calls
- No manual `await app.Use{Module}ModuleAsync()` calls
- No manual `opts.Discovery.IncludeAssembly()` calls
- No manual `opts.PublishMessage<>()` or `ListenToRabbitQueue()` calls

## Module Implementation Patterns

### Standard Module (with EF Core DbContext)

Example: BillingModule (`src/Modules/Billing/Wallow.Billing.Api/BillingModule.cs`)

```csharp
public sealed class BillingModule : IModuleRegistration
{
    public static string ModuleName => "Billing";
    
    public static Assembly? HandlerAssembly =>
        typeof(Application.Commands.CreateInvoice.CreateInvoiceCommand).Assembly;

    public static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddBillingApplication();       // Validators, FluentValidation
        services.AddBillingInfrastructure(configuration);  // DbContext, repositories
    }

    public static async Task InitializeAsync(WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
        await db.Database.MigrateAsync();
    }

    public static void ConfigureMessaging(WolverineOptions options)
    {
        // Publish events
        options.PublishMessage<InvoiceCreatedEvent>().ToRabbitExchange("billing-events");
        options.PublishMessage<InvoicePaidEvent>().ToRabbitExchange("billing-events");
        options.PublishMessage<SubscriptionCreatedEvent>().ToRabbitExchange("billing-events");
        
        // Consume events from other modules
        options.ListenToRabbitQueue("billing-inbox");
    }
}
```

**Typical Infrastructure Layer Extensions** (called by `ConfigureServices`):
```csharp
public static IServiceCollection AddBillingInfrastructure(
    this IServiceCollection services, IConfiguration configuration)
{
    services.AddBillingPersistence(configuration);
    services.AddBillingServices();
    return services;
}

private static IServiceCollection AddBillingPersistence(
    this IServiceCollection services, IConfiguration configuration)
{
    var connectionString = configuration.GetConnectionString("DefaultConnection");
    
    services.AddDbContext<BillingDbContext>((sp, options) =>
    {
        options.UseNpgsql(connectionString, npgsql =>
        {
            npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "billing");
        });
        options.AddInterceptors(sp.GetRequiredService<TenantSaveChangesInterceptor>());
    });
    
    services.AddScoped<IInvoiceRepository, InvoiceRepository>();
    services.AddScoped<ISubscriptionRepository, SubscriptionRepository>();
    
    return services;
}
```

### Stateless Module (no persistence)

Example: A module that wraps external HTTP services with no local persistence.

```csharp
public sealed class ExternalIntegrationModule : IModuleRegistration
{
    public static string ModuleName => "ExternalIntegration";
    public static Assembly? HandlerAssembly => null;  // No CQRS handlers

    public static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddExternalIntegrationInfrastructure(configuration);  // HTTP clients only
    }

    public static Task InitializeAsync(WebApplication app) => Task.CompletedTask;

    public static void ConfigureMessaging(WolverineOptions options) { }  // No messaging
}
```

**Infrastructure Pattern for Stateless Modules**:
```csharp
public static IServiceCollection AddExternalIntegrationInfrastructure(
    this IServiceCollection services, IConfiguration configuration)
{
    // Only HTTP clients and providers, no DbContext
    services.AddHttpClient<ExternalApiClient>();
    services.AddScoped<IExternalProvider, ExternalProvider>();
    services.AddScoped<IExternalProviderFactory, ExternalProviderFactory>();
    return services;
}
```

### Module with Background Jobs

Example: A module that has both persistence and recurring background jobs (e.g., Communications).

```csharp
public sealed class CommunicationsModule : IModuleRegistration
{
    public static string ModuleName => "Communications";

    public static Assembly? HandlerAssembly =>
        typeof(Application.Commands.SendMessage.SendMessageCommand).Assembly;

    public static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddCommunicationsApplication();
        services.AddCommunicationsInfrastructure(configuration);

        // Register recurring jobs
        services.AddSingleton<IRecurringJobRegistration, CommunicationsRecurringJobRegistration>();
    }

    public static async Task InitializeAsync(WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CommunicationsDbContext>();
        await db.Database.MigrateAsync();
    }

    public static void ConfigureMessaging(WolverineOptions options)
    {
        options.PublishMessage<MessageSentEvent>().ToRabbitExchange("communications-events");
        options.ListenToRabbitQueue("communications-inbox");
    }
}
```

**Background Job Registration Pattern**:
```csharp
public class CommunicationsRecurringJobRegistration : IRecurringJobRegistration
{
    public void RegisterJobs()
    {
        RecurringJob.AddOrUpdate<ProcessOutboundMessagesJob>(
            "process-outbound-messages",
            job => job.ExecuteAsync(),
            "*/5 * * * *");  // Every 5 minutes
    }
}
```

Jobs are automatically registered by `HangfireExtensions.RegisterRecurringJobs()` which retrieves all `IRecurringJobRegistration` services.

## Troubleshooting

### Module Not Discovered

**Symptoms**: Module services not registered, handlers not found, module appears to be missing

**Causes & Solutions**:

1. **Assembly not in bin directory**
   - **Check**: Ensure `Wallow.Api.csproj` has a `<ProjectReference>` to the module's Api project
   - **Fix**: Add `<ProjectReference Include="..\..\Modules\{Module}\Wallow.{Module}.Api\Wallow.{Module}.Api.csproj" />`

2. **Missing IModuleRegistration implementation**
   - **Check**: Verify `{Module}Module.cs` exists in the Api project
   - **Fix**: Create the module class implementing `IModuleRegistration`

3. **Wrong assembly name pattern**
   - **Check**: Module Api assembly must follow `Wallow.*.Api.dll` pattern
   - **Fix**: Rename assembly or update discovery pattern in `ModuleDiscovery.cs`

4. **Build error**
   - **Check**: Run `dotnet build` and look for compilation errors
   - **Fix**: Resolve any build errors preventing assembly from being loaded

**Debugging**:
```csharp
// Add to Program.cs temporarily after builder.Build()
var modules = ModuleDiscovery.DiscoverModuleTypes();
foreach (var moduleType in modules)
{
    var moduleName = (string)moduleType.GetProperty("ModuleName")!.GetValue(null)!;
    Console.WriteLine($"Discovered module: {moduleName} ({moduleType.FullName})");
}
```

### Handlers Not Discovered

**Symptoms**: Commands/queries fail with "No handler for message type {MessageType}"

**Causes & Solutions**:

1. **HandlerAssembly returns null**
   - **Check**: Verify `HandlerAssembly` property in module class
   - **Fix**: Set it to the Application assembly: `typeof(Application.Commands.SomeCommand).Assembly`

2. **Wrong assembly returned**
   - **Check**: Ensure the assembly contains the handler classes
   - **Fix**: Point to the correct assembly (usually `Wallow.{Module}.Application`)

3. **Handler naming convention**
   - **Check**: Wolverine expects static methods named `Handle` or `HandleAsync`
   - **Fix**: Rename method or use instance handlers with proper naming

4. **Handler not public**
   - **Check**: Handler class and method must be public
   - **Fix**: Add `public` modifier to class and method

**Debugging**:
```bash
# Enable Wolverine verbose logging in appsettings.Development.json
{
  "Logging": {
    "LogLevel": {
      "Wolverine": "Debug"
    }
  }
}
```

Wolverine will log all discovered handlers at startup.

### Migrations Not Running

**Symptoms**: Tables missing, "relation does not exist" errors, 42P01 PostgreSQL errors

**Causes & Solutions**:

1. **InitializeAsync not calling MigrateAsync**
   - **Check**: Verify `InitializeAsync` implementation
   - **Fix**: Add migration call:
     ```csharp
     public static async Task InitializeAsync(WebApplication app)
     {
         using var scope = app.Services.CreateScope();
         var db = scope.ServiceProvider.GetRequiredService<{Module}DbContext>();
         await db.Database.MigrateAsync();
     }
     ```

2. **Exception swallowed silently**
   - **Check**: Look for "module startup failed" warnings in logs
   - **Fix**: Temporarily remove try-catch to see the actual exception

3. **Wrong DbContext type**
   - **Check**: Ensure correct DbContext type is resolved
   - **Fix**: Use the module's specific DbContext, not base DbContext

4. **Migration files missing**
   - **Check**: Verify `Migrations/` folder exists in Infrastructure project
   - **Fix**: Run `dotnet ef migrations add InitialCreate` for the module

5. **Connection string incorrect**
   - **Check**: Verify `appsettings.Development.json` has `DefaultConnection`
   - **Fix**: Add connection string or check PostgreSQL is running

**Debugging**:
```csharp
// Temporarily modify InitializeAsync to see exceptions
public static async Task InitializeAsync(WebApplication app)
{
    var logger = app.Services.GetRequiredService<ILogger<{Module}Module>>();
    try
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<{Module}DbContext>();
        logger.LogInformation("Running {Module} migrations...");
        await db.Database.MigrateAsync();
        logger.LogInformation("{Module} migrations complete");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "{Module} migration failed");
        throw;  // Temporarily re-throw to see the error
    }
}
```

### Messaging Not Working

**Symptoms**: Events not published, consumers not receiving messages, RabbitMQ queues not created

**Causes & Solutions**:

1. **ConfigureMessaging empty**
   - **Check**: Verify `ConfigureMessaging` has publish/listen configurations
   - **Fix**: Add routing:
     ```csharp
     public static void ConfigureMessaging(WolverineOptions options)
     {
         options.PublishMessage<SomeEvent>().ToRabbitExchange("{module}-events");
         options.ListenToRabbitQueue("{module}-inbox");
     }
     ```

2. **Exchange not created in RabbitMQ**
   - **Check**: Open RabbitMQ management UI (http://localhost:15672), go to Exchanges
   - **Fix**: Ensure `AutoProvision()` is called on RabbitMQ configuration in Program.cs
   - **Verify**: Exchange named `{module}-events` should exist

3. **Queue not bound to exchange**
   - **Check**: RabbitMQ management UI → Queues → Click queue → Bindings section
   - **Fix**: Ensure routing key matches event type name
   - **Wolverine auto-binds**: Should happen automatically if AutoProvision is enabled

4. **Wrong event type in contracts**
   - **Check**: Verify event type in `Shared.Contracts` matches what's being published
   - **Fix**: Ensure namespace and type name are identical

5. **RabbitMQ not running**
   - **Check**: `docker ps | grep rabbitmq`
   - **Fix**: `cd docker && docker compose up -d rabbitmq`

**Debugging**:
```csharp
// Add to ConfigureMessaging to see what's registered
public static void ConfigureMessaging(WolverineOptions options)
{
    var logger = options.Services.BuildServiceProvider()
        .GetRequiredService<ILogger<{Module}Module>>();
    
    logger.LogInformation("Configuring {Module} messaging...");
    
    options.PublishMessage<SomeEvent>().ToRabbitExchange("{module}-events");
    options.ListenToRabbitQueue("{module}-inbox");
    
    logger.LogInformation("{Module} messaging configured");
}
```

Check RabbitMQ Management UI:
- Exchanges tab: Should see `{module}-events` exchange
- Queues tab: Should see `{module}-inbox` queue
- Click queue → Bindings: Should see binding to exchange

### Service Not Resolved (Dependency Injection)

**Symptoms**: "Unable to resolve service for type {ServiceType}"

**Causes & Solutions**:

1. **Service not registered**
   - **Check**: Verify service registration in Infrastructure extensions
   - **Fix**: Add `services.AddScoped<IService, ServiceImplementation>()`

2. **Wrong lifetime**
   - **Check**: Singleton trying to inject scoped service
   - **Fix**: Match lifetimes (Scoped → Scoped, Transient → Scoped/Transient)

3. **Circular dependency**
   - **Check**: Service A depends on B, B depends on A
   - **Fix**: Extract interface or use mediator pattern

4. **Registration order**
   - **Check**: Some services must be registered before others
   - **Fix**: Ensure dependencies are registered first

**Debugging**:
```bash
# Enable DI validation in Program.cs (Development only)
builder.Services.AddControllers();
if (builder.Environment.IsDevelopment())
{
    builder.Services.BuildServiceProvider(new ServiceProviderOptions
    {
        ValidateScopes = true,
        ValidateOnBuild = true
    });
}
```

## Database Schema Isolation

Each module owns its PostgreSQL schema:

| Module | Schema | Type | Notes |
|--------|--------|------|-------|
| Identity | `identity` | EF Core | Multi-tenancy interceptor |
| Billing | `billing` | EF Core | Multi-tenancy interceptor |
| Communications | `communications` | EF Core | Multi-tenancy interceptor |
| Configuration | `configuration` | EF Core | Multi-tenancy interceptor |
| Storage | `storage` | EF Core | Multi-tenancy interceptor |
| Wolverine | `wolverine` | Shared Infrastructure | Outbox/inbox |
| Hangfire | `hangfire` | Shared Infrastructure | Job storage |

**Migration Pattern**:
```csharp
options.UseNpgsql(connectionString, npgsql =>
{
    npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "{schema_name}");
});
```

## Multi-Tenancy Integration

**Pattern**: TenantSaveChangesInterceptor

```csharp
services.AddDbContext<{Module}DbContext>((sp, options) =>
{
    options.UseNpgsql(connectionString);
    options.AddInterceptors(sp.GetRequiredService<TenantSaveChangesInterceptor>());
});
```

- Automatically stamps `TenantId` on save for entities implementing `ITenantEntity`
- Registered in: `Identity.Infrastructure.Extensions`
- All DbContexts use it (note: singleton-lifetime DbContexts cannot use this interceptor)
- Resolves current tenant from `ITenantContext`

**Tenant Resolution Flow**:
1. `TenantResolutionMiddleware` extracts tenant ID from JWT or header
2. Sets `ITenantContext.Current`
3. `TenantSaveChangesInterceptor` reads `ITenantContext.Current` on save
4. Stamps `TenantId` on new entities

## Legacy Extension Methods (Deprecated)

**Status**: `[Obsolete]` warnings added to old extension methods

The old manual registration pattern used these extension methods:
- `builder.Services.Add{Module}Module(configuration)` (IServiceCollection extension)
- `await app.Use{Module}ModuleAsync()` (WebApplication extension)

**Migration Path**:
- **Old**: Explicit calls in `Program.cs` for each module
- **New**: Single call to `AddWallowModules()` and `UseWallowModulesAsync()`

**What Happens to Legacy Extensions**:
- They still exist and are called internally by the `{Module}Module` class
- `ConfigureServices()` calls `Add{Module}Infrastructure()`
- `InitializeAsync()` contains the same logic as `Use{Module}ModuleAsync()`
- They are NOT called from `Program.cs` anymore

**If You See Obsolete Warnings**:
- The code still works
- No action required if using auto-discovery
- Only affects custom/manual registration scenarios

**Example Legacy Code**:
```csharp
// OLD PATTERN (Program.cs) - DEPRECATED
builder.Services.AddBillingModule(builder.Configuration);
await app.UseBillingModuleAsync();

// NEW PATTERN (Program.cs) - CURRENT
builder.Services.AddWallowModules(builder.Configuration);  // Discovers all modules
await app.UseWallowModulesAsync();  // Initializes all modules
```

## Module Inventory

| Module | Type | IModuleRegistration | HandlerAssembly | ConfigureMessaging | Background Jobs |
|--------|------|---------------------|-----------------|-------------------|-----------------|
| Identity | Standard | Yes | No | Yes | No |
| Billing | Standard | Yes | Yes | Yes | No |
| Communications | Standard | Yes | Yes | Yes | No |
| Configuration | Standard | Yes | Yes | Yes | No |
| Storage | Standard | Yes | Yes | Yes | No |

## Key Patterns and Best Practices

### 1. Module Class Naming
- Standard: `{Module}Module` (e.g., `BillingModule`)
- Location: `src/Modules/{Module}/Wallow.{Module}.Api/{Module}Module.cs`
- Must be public and sealed
- Must implement `IModuleRegistration`

### 2. Handler Assembly Property
- Return the Application assembly if module has CQRS handlers
- Return `null` if module has no handlers (stateless modules)
- Pattern: `typeof(Application.Commands.SomeCommand).Assembly`

### 3. Error Handling in InitializeAsync
- Log migration failures as warnings, not exceptions
- Use try-catch to prevent module startup failures from crashing the app
- Pattern:
  ```csharp
  public static async Task InitializeAsync(WebApplication app)
  {
      try
      {
          using var scope = app.Services.CreateScope();
          var db = scope.ServiceProvider.GetRequiredService<{Module}DbContext>();
          await db.Database.MigrateAsync();
      }
      catch (Exception ex)
      {
          var logger = app.Services.GetRequiredService<ILogger<{Module}Module>>();
          logger.LogWarning(ex, "{Module} module startup failed. Ensure PostgreSQL is running.", ModuleName);
      }
  }
  ```

### 4. Configuration Pattern
- Get connection string: `configuration.GetConnectionString("DefaultConnection")`
- Get typed options: `configuration.GetSection("SectionName").Get<OptionsType>()`
- Pass configuration to Infrastructure extensions

### 5. Service Lifetime Patterns
Standard conventions:
- `AddScoped<IRepository, Implementation>()` - DbContext-dependent queries
- `AddScoped<IService, Implementation>()` - Application services
- `AddSingleton<ICache, Implementation>()` - Stateless caches
- `AddTransient<IDataExporter, Implementation>()` - Stateless utilities
- `AddSingleton<IRecurringJobRegistration, Implementation>()` - Background jobs

### 6. Messaging Conventions
- Event exchange naming: `{module}-events` (lowercase)
- Consumer queue naming: `{module}-inbox` (lowercase)
- Publish all module events to single exchange
- Listen to single inbox queue for consuming events from other modules

## Module Dependency Graph

```
Wallow.Api (composition root)
├─ Identity → Keycloak, JWT, TenantContext, Permissions
├─ Billing → Invoices, Subscriptions, Payment processing
├─ Communications → Messaging, notifications, email delivery
├─ Configuration → Tenant/system configuration, feature flags
└─ Storage → S3/Local file storage

All modules share:
├─ Wallow.Shared.Kernel → Base types, ITenantContext, Result<T>, IModuleRegistration
├─ Wallow.Shared.Contracts → Integration events
├─ Wallow.Shared.Infrastructure → ModuleDiscovery, common services
├─ Wolverine → CQRS mediator, RabbitMQ transport
├─ EF Core → Multi-schema, auto-migrations
├─ PostgreSQL → All data storage
├─ Hangfire → Background jobs
└─ RabbitMQ → Event-driven communication
```

## Initialization Order

1. **Wolverine Setup** - Handlers, RabbitMQ routing
2. **Redis SignalR Backplane** - Real-time communication
3. **Core Services** - HttpContext, Controllers, Kernel services
4. **Module Service Registration** - `AddWallowModules()` discovers and registers all modules
5. **Hangfire Setup** - Background job infrastructure
6. **App Build** - Build the WebApplication
7. **Module Initialization** - `UseWallowModulesAsync()` runs migrations and seeders
8. **Recurring Job Registration** - `RegisterRecurringJobs()` discovers all IRecurringJobRegistration
9. **Middleware Pipeline** - Auth, tenant resolution, exception handling
10. **Endpoint Mapping** - Controllers, SignalR hubs

---

**Last Updated**: 2026-02-13  
**Framework**: .NET 10.0  
**Architecture**: Modular Monolith with Clean Architecture, DDD, CQRS, Auto-Discovery
