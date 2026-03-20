using Wallow.Messaging.IntegrationTests.TestEvents;
using Microsoft.Extensions.Logging;

namespace Wallow.Messaging.IntegrationTests.TestHandlers;

#pragma warning disable CA1848, CA1873
public class TestEventHandler
{
    private readonly ILogger<TestEventHandler> _logger;
    private readonly IMessageTracker _tracker;

    public TestEventHandler(ILogger<TestEventHandler> logger, IMessageTracker tracker)
    {
        _logger = logger;
        _tracker = tracker;
    }

    public Task Handle(TestEvent @event)
    {
        _tracker.RecordEvent(@event);
        _logger.LogInformation("Processed TestEvent: {Message}, Counter: {Counter}",
            @event.Message, @event.Counter);
        return Task.CompletedTask;
    }

    public Task Handle(TestEventThatFails @event)
    {
        _tracker.IncrementAttempt(@event.EventId);
        int attempts = _tracker.GetAttemptCount(@event.EventId);

        _logger.LogWarning("Processing TestEventThatFails attempt {Attempt} of {FailAfter}",
            attempts, @event.FailAfterAttempts);

        if (attempts < @event.FailAfterAttempts)
        {
            throw new InvalidOperationException($"Simulated failure on attempt {attempts}");
        }

        _logger.LogInformation("TestEventThatFails succeeded after {Attempts} attempts", attempts);
        return Task.CompletedTask;
    }

    public Task Handle(TestEventThatFailsImmediately @event)
    {
        _logger.LogError("Processing TestEventThatFailsImmediately - throwing ArgumentException");
        throw new ArgumentException($"Simulated immediate failure: {@event.Message}");
    }
}
#pragma warning restore CA1848, CA1873
