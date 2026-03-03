using Foundry.Messaging.IntegrationTests.TestEvents;

namespace Foundry.Messaging.IntegrationTests.TestHandlers;

public class MessageTracker : IMessageTracker
{
    private readonly List<TestEvent> _processedEvents = new();
    private readonly Dictionary<Guid, int> _attemptCounts = new();
    private readonly Lock _lock = new();

    public void RecordEvent(TestEvent @event)
    {
        using (_lock.EnterScope())
        {
            _processedEvents.Add(@event);
        }
    }

    public void IncrementAttempt(Guid eventId)
    {
        using (_lock.EnterScope())
        {
            if (!_attemptCounts.TryGetValue(eventId, out int count))
            {
                count = 0;
            }
            _attemptCounts[eventId] = count + 1;
        }
    }

    public IReadOnlyList<TestEvent> GetProcessedEvents()
    {
        using (_lock.EnterScope())
        {
            return _processedEvents.ToList();
        }
    }

    public int GetAttemptCount(Guid eventId)
    {
        using (_lock.EnterScope())
        {
            return _attemptCounts.GetValueOrDefault(eventId, 0);
        }
    }

    public void Reset()
    {
        using (_lock.EnterScope())
        {
            _processedEvents.Clear();
            _attemptCounts.Clear();
        }
    }
}
