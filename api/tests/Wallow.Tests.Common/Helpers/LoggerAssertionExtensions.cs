using Microsoft.Extensions.Logging;
using NSubstitute.Core;

namespace Wallow.Tests.Common.Helpers;

public static class LoggerAssertionExtensions
{
    public static bool LogMessageContains(ICall call, string message)
    {
        object? state = call.GetArguments()[2];
        return state != null && state.ToString()!.Contains(message);
    }

    public static void ShouldHaveLoggedMessage(this ILogger logger, string message)
    {
        List<ICall> calls = logger.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == "Log")
            .Where(c => LogMessageContains(c, message))
            .ToList();

        calls.Should().HaveCountGreaterThanOrEqualTo(1,
            $"expected at least one log call containing '{message}'");
    }
}
