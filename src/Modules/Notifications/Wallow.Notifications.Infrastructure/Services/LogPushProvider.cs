using Wallow.Notifications.Application.Channels.Push.Interfaces;
using Wallow.Notifications.Domain.Channels.Push.Entities;
using Microsoft.Extensions.Logging;

namespace Wallow.Notifications.Infrastructure.Services;

public sealed partial class LogPushProvider(ILogger<LogPushProvider> logger) : IPushProvider
{
    public Task<PushDeliveryResult> SendAsync(PushMessage message, string deviceToken, CancellationToken cancellationToken = default)
    {
        LogPushSuppressed(logger, deviceToken, message.Title, message.Body);
        return Task.FromResult(new PushDeliveryResult(true, null));
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Push suppressed (LogPushProvider) to device {DeviceToken}: {Title} - {Body}")]
    private static partial void LogPushSuppressed(ILogger logger, string deviceToken, string title, string body);
}
