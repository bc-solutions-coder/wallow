using Foundry.Messaging.IntegrationTests.TestEvents;

namespace Foundry.Messaging.IntegrationTests.TestHandlers;

public class MessageTracker : IMessageTracker
{
    private readonly List<TestEvent> _processedEvents = new();
    private readonly Dictionary<Guid, int> _attemptCounts = new();
    private readonly object _lock = new();

    public void RecordEvent(TestEvent @event)
    {
        lock (_lock)
        {
            _processedEvents.Add(@event);
        }
    }

    public void IncrementAttempt(Guid eventId)
    {
        lock (_lock)
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
        lock (_lock)
        {
            return _processedEvents.ToList();
        }
    }

    public int GetAttemptCount(Guid eventId)
    {
        lock (_lock)
        {
            return _attemptCounts.GetValueOrDefault(eventId, 0);
        }
    }

    public void Reset()
    {
        lock (_lock)
        {
            _processedEvents.Clear();
            _attemptCounts.Clear();
        }
    }
}
