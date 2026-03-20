using Wallow.Shared.Contracts.Identity.Events;
using Microsoft.Extensions.Logging;

namespace Wallow.Messaging.IntegrationTests.TestHandlers;

public sealed class UserRegisteredEventTestHandler
{
    public static Task HandleAsync(
        UserRegisteredEvent integrationEvent,
        ICrossModuleEventTracker tracker,
        ILogger<UserRegisteredEventTestHandler> logger,
        CancellationToken _)
    {
#pragma warning disable CA1848, CA1873
        logger.LogInformation(
            "Test handler processing UserRegisteredEvent for User {UserId}",
            integrationEvent.UserId);
#pragma warning restore CA1848, CA1873

        tracker.RecordHandlerExecution("Test", "UserRegistered", integrationEvent.EventId);

        return Task.CompletedTask;
    }
}
