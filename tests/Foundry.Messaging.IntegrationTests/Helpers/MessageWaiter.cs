using Foundry.Messaging.IntegrationTests.TestHandlers;

namespace Foundry.Messaging.IntegrationTests.Helpers;

public class MessageWaiter
{
    private readonly IMessageTracker _tracker;
    private readonly ICrossModuleEventTracker _crossModuleTracker;
    private const int DefaultTimeoutMs = 10000;
    private const int PollingIntervalMs = 100;

    public MessageWaiter(IMessageTracker tracker, ICrossModuleEventTracker crossModuleTracker)
    {
        _tracker = tracker;
        _crossModuleTracker = crossModuleTracker;
    }

    public async Task WaitForMessageAsync(
        Func<bool> condition,
        int timeoutMs = DefaultTimeoutMs,
        CancellationToken cancellationToken = default)
    {
        DateTime deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);

        while (DateTime.UtcNow < deadline)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(PollingIntervalMs, cancellationToken);
        }

        throw new TimeoutException($"Condition not met within {timeoutMs}ms");
    }

    public Task WaitForEventCountAsync(
        int expectedCount,
        int timeoutMs = DefaultTimeoutMs,
        CancellationToken cancellationToken = default)
    {
        return WaitForMessageAsync(
            () => _tracker.GetProcessedEvents().Count >= expectedCount,
            timeoutMs,
            cancellationToken);
    }

    public Task WaitForAttemptCountAsync(
        Guid eventId,
        int expectedAttempts,
        int timeoutMs = DefaultTimeoutMs,
        CancellationToken cancellationToken = default)
    {
        return WaitForMessageAsync(
            () => _tracker.GetAttemptCount(eventId) >= expectedAttempts,
            timeoutMs,
            cancellationToken);
    }

    public Task WaitForCrossModuleHandlersAsync(
        string eventType,
        Guid eventId,
        int expectedHandlerCount,
        int timeoutMs = DefaultTimeoutMs,
        CancellationToken cancellationToken = default)
    {
        return WaitForMessageAsync(
            () => _crossModuleTracker.GetHandlerCount(eventType, eventId) >= expectedHandlerCount,
            timeoutMs,
            cancellationToken);
    }
}
