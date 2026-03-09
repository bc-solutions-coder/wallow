// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace Foundry.Messaging.IntegrationTests.TestHandlers;

public class CrossModuleEventTracker : ICrossModuleEventTracker
{
    private readonly List<HandlerExecution> _executions = new();
    private readonly Lock _lock = new();

    public void RecordHandlerExecution(string module, string eventType, Guid eventId)
    {
        using (_lock.EnterScope())
        {
            _executions.Add(new HandlerExecution
            {
                Module = module,
                EventType = eventType,
                EventId = eventId
            });
        }
    }

    public IReadOnlyList<string> GetExecutedHandlers(string eventType, Guid eventId)
    {
        using (_lock.EnterScope())
        {
            return _executions
                .Where(e => e.EventType == eventType && e.EventId == eventId)
                .Select(e => e.Module)
                .ToList();
        }
    }

    public int GetHandlerCount(string eventType, Guid eventId)
    {
        using (_lock.EnterScope())
        {
            return _executions.Count(e => e.EventType == eventType && e.EventId == eventId);
        }
    }

    public void Reset()
    {
        using (_lock.EnterScope())
        {
            _executions.Clear();
        }
    }

    private sealed class HandlerExecution
    {
        public required string Module { get; init; }
        public required string EventType { get; init; }
        public required Guid EventId { get; init; }
    }
}
