using Microsoft.Extensions.Logging;
using Wolverine;
using Wolverine.ErrorHandling;

namespace Foundry.Shared.Infrastructure.Messaging;

public static class WolverineErrorHandlingExtensions
{
    /// <summary>
    /// Configures standard error handling policies for Wolverine:
    /// - Retry with exponential backoff for transient failures
    /// - Move to dead letter queue after max retries
    /// - Log all failures
    /// </summary>
    public static void ConfigureStandardErrorHandling(this WolverineOptions opts)
    {
        opts.Policies.OnException<TimeoutException>()
            .RetryWithCooldown(TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(250))
            .Then.MoveToErrorQueue();

        opts.Policies.OnException<InvalidOperationException>()
            .RetryTimes(2)
            .Then.MoveToErrorQueue();

        // All other exceptions - retry once, then DLQ
        opts.Policies.OnAnyException()
            .RetryTimes(1)
            .Then.MoveToErrorQueue();
    }

    /// <summary>
    /// Configures message logging for Wolverine:
    /// - Logs message execution start/end
    /// - Logs failures
    /// </summary>
    public static void ConfigureMessageLogging(this WolverineOptions opts)
    {
        opts.Policies.LogMessageStarting(LogLevel.Debug);
    }
}
