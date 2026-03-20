# Workflows Guide

This guide covers the Elsa Workflows integration in Wallow, which provides a shared infrastructure capability for building automated workflows within modules.

---

## Overview

### What is Elsa Workflows?

Wallow integrates [Elsa Workflows 3](https://elsa-workflows.github.io/elsa-core/), a .NET workflow engine. The integration lives in **Shared Infrastructure** (not a standalone module) and provides:

- **`WorkflowActivityBase`** - A base class for modules to define custom workflow activities with module-scoped logging
- **`ElsaExtensions.AddWallowWorkflows()`** - Centralized Elsa configuration with PostgreSQL persistence and auto-discovery of module activities
- **Module-owned activities** - Each module can define its own workflow activities by extending `WorkflowActivityBase`

### Architecture

Workflows are a **shared infrastructure capability**, not a separate module. The integration consists of:

```
src/Shared/Wallow.Shared.Infrastructure/Workflows/
    WorkflowActivityBase.cs     # Base class for module activities
    ElsaExtensions.cs           # Elsa DI registration and configuration
```

Modules define their own activities in their Infrastructure layer:

```
src/Modules/Billing/Wallow.Billing.Infrastructure/Workflows/
    InvoiceCreatedTrigger.cs    # Example: Billing module activity
```

### When to Use Workflows

| Use Case | Use Workflows | Alternative |
|----------|---------------|-------------|
| Multi-step processes with branching | Yes | Direct code handlers |
| Visual workflow design (future) | Yes | - |
| Scheduled/timer-based automation | Yes | Hangfire jobs |
| Simple event reactions | No | Wolverine event handlers |
| High-frequency operations | No | Direct code |
| Core business logic | No | Domain model |

---

## WorkflowActivityBase

The `WorkflowActivityBase` class wraps Elsa's `CodeActivity` with module-scoped logging and context:

```csharp
// src/Shared/Wallow.Shared.Infrastructure/Workflows/WorkflowActivityBase.cs
public abstract class WorkflowActivityBase : CodeActivity
{
    /// <summary>
    /// The name of the module this activity belongs to. Override in derived classes.
    /// </summary>
    public virtual string ModuleName => "Shared";

    protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        ILogger logger = context.GetRequiredService<ILoggerFactory>()
            .CreateLogger(GetType());

        using (logger.BeginScope(new Dictionary<string, object>
        {
            ["Module"] = ModuleName,
            ["ActivityType"] = GetType().Name,
            ["WorkflowInstanceId"] = context.WorkflowExecutionContext.Id
        }))
        {
            // Logs execution start/end with module context
            await ExecuteActivityAsync(context);
        }
    }

    /// <summary>
    /// Override this method to implement the activity's logic.
    /// </summary>
    protected abstract ValueTask ExecuteActivityAsync(ActivityExecutionContext context);
}
```

---

## Creating Custom Activities

Modules create workflow activities by extending `WorkflowActivityBase`:

```csharp
// src/Modules/Billing/Wallow.Billing.Infrastructure/Workflows/InvoiceCreatedTrigger.cs
using Elsa.Workflows;
using Wallow.Shared.Infrastructure.Workflows;

public class InvoiceCreatedTrigger : WorkflowActivityBase
{
    public override string ModuleName => "Billing";

    protected override ValueTask ExecuteActivityAsync(ActivityExecutionContext context)
    {
        // Activity logic here
        return ValueTask.CompletedTask;
    }
}
```

### Auto-Discovery

Activities extending `WorkflowActivityBase` are automatically discovered from all `Wallow.*` assemblies at startup. No manual registration is needed:

```csharp
// ElsaExtensions.cs - auto-discovers activities
IEnumerable<Type> activityTypes = AppDomain.CurrentDomain.GetAssemblies()
    .Where(a => a.GetName().Name?.StartsWith("Wallow.", StringComparison.Ordinal) == true)
    .SelectMany(a => a.GetExportedTypes())
    .Where(t => t is { IsAbstract: false, IsInterface: false }
        && t.IsAssignableTo(typeof(WorkflowActivityBase)));

management.AddActivities(activityTypes);
```

---

## Configuration

### Elsa Registration

Elsa is configured via `AddWallowWorkflows()` in Program.cs:

```csharp
// src/Shared/Wallow.Shared.Infrastructure/Workflows/ElsaExtensions.cs
public static IServiceCollection AddWallowWorkflows(
    this IServiceCollection services,
    IConfiguration configuration,
    IHostEnvironment environment)
{
    services.AddElsa(elsa =>
    {
        // PostgreSQL persistence for workflow definitions and runtime
        elsa.UseWorkflowManagement(management =>
        {
            management.UseEntityFrameworkCore(ef => ef.UsePostgreSql(connectionString));
            management.AddActivities(activityTypes); // Auto-discovered
        });

        elsa.UseWorkflowRuntime(runtime =>
            runtime.UseEntityFrameworkCore(ef => ef.UsePostgreSql(connectionString)));

        elsa.UseIdentity(identity =>
        {
            identity.TokenOptions = options => options.SigningKey = signingKey;
            identity.UseAdminUserProvider();
        });

        elsa.UseWorkflowsApi();
        elsa.UseScheduling();
        elsa.UseHttp();
        elsa.UseEmail(email =>
            email.ConfigureOptions = options => configuration.GetSection("Elsa:Smtp").Bind(options));
    });

    return services;
}
```

### appsettings.json

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=wallow;Username=postgres;Password=postgres"
  },
  "Elsa": {
    "Identity": {
      "SigningKey": "your-signing-key-here"
    },
    "Smtp": {
      "Host": "localhost",
      "Port": 1025
    }
  }
}
```

> **Note:** In development, a default signing key is used automatically. In production, `Elsa:Identity:SigningKey` must be explicitly configured.

### Dependencies

| Package | Version | Purpose |
|---------|---------|---------|
| `Elsa` | 3.5.3 | Core workflow engine |
| `Elsa.EntityFrameworkCore.PostgreSql` | 3.5.3 | PostgreSQL persistence |
| `Elsa.Scheduling` | 3.5.3 | Timer/cron support |
| `Elsa.Email` | 3.5.3 | Email activities |
| `Elsa.Http` | 3.5.3 | HTTP triggers/activities |

---

## Adding Activities from a Module

To add a workflow activity from your module:

1. **Add a reference** to `Wallow.Shared.Infrastructure` (if not already present)

2. **Create your activity** in `Infrastructure/Workflows/`:

```csharp
// src/Modules/{Module}/Wallow.{Module}.Infrastructure/Workflows/MyActivity.cs
using Elsa.Workflows;
using Wallow.Shared.Infrastructure.Workflows;

public class MyActivity : WorkflowActivityBase
{
    public override string ModuleName => "{Module}";

    protected override async ValueTask ExecuteActivityAsync(ActivityExecutionContext context)
    {
        // Access DI services
        var service = context.GetRequiredService<IMyService>();

        // Implement activity logic
        await service.DoWorkAsync(context.CancellationToken);
    }
}
```

3. **No registration needed** - The activity will be auto-discovered at startup

---

## Related Documentation

- [Elsa Workflows Documentation](https://elsa-workflows.github.io/elsa-core/)
- [Developer Guide](DEVELOPER_GUIDE.md)
- [Background Jobs Guide](BACKGROUND_JOBS_GUIDE.md) - For time-based background work
- [Messaging Guide](MESSAGING_GUIDE.md) - For event-driven processing
