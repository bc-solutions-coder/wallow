using Elsa.Workflows;
using Microsoft.Extensions.Logging;

namespace Foundry.Shared.Infrastructure.Workflows;

/// <summary>
/// Base class for module-specific workflow activities.
/// Wraps execution with module-scoped logging and context.
/// </summary>
public abstract class WorkflowActivityBase : CodeActivity
{
    private static readonly Action<ILogger, string, string, Exception?> _logExecutingActivity =
        LoggerMessage.Define<string, string>(LogLevel.Debug, new EventId(1, nameof(_logExecutingActivity)),
            "Executing workflow activity {ActivityType} in module {Module}");

    private static readonly Action<ILogger, string, string, Exception?> _logCompletedActivity =
        LoggerMessage.Define<string, string>(LogLevel.Debug, new EventId(2, nameof(_logCompletedActivity)),
            "Completed workflow activity {ActivityType} in module {Module}");

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
            _logExecutingActivity(logger, GetType().Name, ModuleName, null);

            await ExecuteActivityAsync(context);

            _logCompletedActivity(logger, GetType().Name, ModuleName, null);
        }
    }

    /// <summary>
    /// Override this method to implement the activity's logic.
    /// </summary>
    protected abstract ValueTask ExecuteActivityAsync(ActivityExecutionContext context);
}
