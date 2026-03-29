# Workflows

Wallow integrates [Elsa Workflows 3](https://elsa-workflows.github.io/elsa-core/) as a shared infrastructure capability for building automated workflows within modules.

## Overview

The integration provides:

- **`WorkflowActivityBase`** -- a base class for modules to define custom workflow activities with module-scoped logging
- **`ElsaExtensions.AddWallowWorkflows()`** -- centralized Elsa configuration with PostgreSQL persistence and auto-discovery of module activities
- **Module-owned activities** -- each module defines its own workflow activities by extending `WorkflowActivityBase`

### Project Structure

The workflow infrastructure lives in a dedicated shared project:

```
src/Shared/Wallow.Shared.Infrastructure.Workflows/Workflows/
    WorkflowActivityBase.cs     # Base class for module activities
    ElsaExtensions.cs           # Elsa DI registration and configuration
```

Modules define their own activities in their Infrastructure layer:

```
src/Modules/Billing/Wallow.Billing.Infrastructure/Workflows/
    InvoiceCreatedTrigger.cs    # Billing module activity
```

### When to Use Workflows

| Use Case | Use Workflows | Alternative |
|----------|---------------|-------------|
| Multi-step processes with branching | Yes | Direct code handlers |
| Scheduled/timer-based automation | Yes | Hangfire jobs |
| Simple event reactions | No | Wolverine event handlers |
| High-frequency operations | No | Direct code |
| Core business logic | No | Domain model |

## WorkflowActivityBase

`WorkflowActivityBase` (`src/Shared/Wallow.Shared.Infrastructure.Workflows/Workflows/WorkflowActivityBase.cs`) wraps Elsa's `CodeActivity` with module-scoped logging:

```csharp
public abstract class WorkflowActivityBase : CodeActivity
{
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
            await ExecuteActivityAsync(context);
        }
    }

    protected abstract ValueTask ExecuteActivityAsync(ActivityExecutionContext context);
}
```

## Creating Custom Activities

Modules create workflow activities by extending `WorkflowActivityBase`:

```csharp
// src/Modules/Billing/Wallow.Billing.Infrastructure/Workflows/InvoiceCreatedTrigger.cs
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

Activities extending `WorkflowActivityBase` are automatically discovered from all `Wallow.*` assemblies at startup. No manual registration is needed.

## Configuration

### Registration

Elsa is registered via `AddWallowWorkflows()` in `Program.cs`. The workflow engine is disabled in the Testing environment and can be disabled via the `Elsa:Enabled` configuration key.

Key configuration from `ElsaExtensions` (`src/Shared/Wallow.Shared.Infrastructure.Workflows/Workflows/ElsaExtensions.cs`):

- PostgreSQL persistence for workflow definitions and runtime (using `DefaultConnection`)
- Auto-discovery of module activities from all `Wallow.*` assemblies
- Identity with a configurable signing key (defaults to a placeholder in development)
- Scheduling, HTTP triggers, and email activities
- Workflow management API (development only)

### appsettings.json

```json
{
  "Elsa": {
    "Enabled": true,
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

In development, a default signing key is used automatically. In production, `Elsa:Identity:SigningKey` must be explicitly configured.

### Dependencies

| Package | Version | Purpose |
|---------|---------|---------|
| `Elsa` | 3.6.0 | Core workflow engine |
| `Elsa.Persistence.EFCore.PostgreSql` | 3.6.0 | PostgreSQL persistence |
| `Elsa.Scheduling` | 3.6.0 | Timer/cron support |
| `Elsa.Email` | 3.6.0 | Email activities |
| `Elsa.Http` | 3.6.0 | HTTP triggers/activities |
| `Elsa.Identity` | 3.6.0 | Identity and token management |
| `Elsa.Workflows.Api` | 3.6.0 | Management API (dev only) |

## Adding Activities from a Module

1. Add a project reference to `Wallow.Shared.Infrastructure.Workflows` (if not already present)
2. Create your activity class in `Infrastructure/Workflows/`, extending `WorkflowActivityBase`
3. Override `ModuleName` and implement `ExecuteActivityAsync`
4. No registration needed -- the activity is auto-discovered at startup

## Related Documentation

- [Elsa Workflows Documentation](https://elsa-workflows.github.io/elsa-core/)
- [Background Jobs Guide](background-jobs.md)
- [Messaging Guide](messaging.md)
