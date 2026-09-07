using Microsoft.Extensions.Logging;
using Wolverine;
using Wolverine.ErrorHandling;

namespace Wallow.Shared.Infrastructure.Core.Messaging;

public static class WolverineErrorHandlingExtensions
{
    /// <summary>
    /// Retries timeouts after 50, 100 and 250 ms; invalid operations twice; other errors once.
    /// Exhausted retries move to the error queue.
    /// </summary>
    public static void ConfigureStandardErrorHandling(this WolverineOptions opts)
    {
        opts.Policies.OnException<TimeoutException>()
            .RetryWithCooldown(TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(250))
            .Then.MoveToErrorQueue();

        opts.Policies.OnException<InvalidOperationException>()
            .RetryTimes(2)
            .Then.MoveToErrorQueue();

        opts.Policies.OnAnyException()
            .RetryTimes(1)
            .Then.MoveToErrorQueue();
    }

    /// <summary>
    /// Logs message starts at Debug level.
    /// </summary>
    public static void ConfigureMessageLogging(this WolverineOptions opts)
    {
        opts.Policies.LogMessageStarting(LogLevel.Debug);
    }
}
