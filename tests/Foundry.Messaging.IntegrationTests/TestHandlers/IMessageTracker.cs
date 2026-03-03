using Foundry.Messaging.IntegrationTests.TestEvents;

namespace Foundry.Messaging.IntegrationTests.TestHandlers;

public interface IMessageTracker
{
    void RecordEvent(TestEvent @event);
    void IncrementAttempt(Guid eventId);
    IReadOnlyList<TestEvent> GetProcessedEvents();
    int GetAttemptCount(Guid eventId);
    void Reset();
}
