namespace Wallow.Messaging.IntegrationTests.TestHandlers;

public interface ICrossModuleEventTracker
{
    void RecordHandlerExecution(string module, string eventType, Guid eventId);
    IReadOnlyList<string> GetExecutedHandlers(string eventType, Guid eventId);
    int GetHandlerCount(string eventType, Guid eventId);
    void Reset();
}
