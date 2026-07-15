namespace Wallow.Api.Jobs;

internal sealed partial class SystemHeartbeatJob
{
    private readonly ILogger<SystemHeartbeatJob> _logger;

    public SystemHeartbeatJob(ILogger<SystemHeartbeatJob> logger)
    {
        _logger = logger;
    }

    public Task ExecuteAsync()
    {
        LogHeartbeat(_logger, DateTime.UtcNow);
        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Heartbeat: system alive at {Timestamp}")]
    private static partial void LogHeartbeat(ILogger logger, DateTime timestamp);
}
