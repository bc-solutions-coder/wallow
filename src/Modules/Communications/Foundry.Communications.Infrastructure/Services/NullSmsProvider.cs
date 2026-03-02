using Foundry.Communications.Application.Channels.Sms.Interfaces;
using Microsoft.Extensions.Logging;

namespace Foundry.Communications.Infrastructure.Services;

public sealed partial class NullSmsProvider(ILogger<NullSmsProvider> logger) : ISmsProvider
{
    public Task<SmsDeliveryResult> SendAsync(string to, string body, CancellationToken cancellationToken = default)
    {
        LogSmsSuppressed(logger, to, body);
        return Task.FromResult(new SmsDeliveryResult(true, "null-sid", null));
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "SMS suppressed (NullSmsProvider) to {To}: {Body}")]
    private static partial void LogSmsSuppressed(ILogger logger, string to, string body);
}
