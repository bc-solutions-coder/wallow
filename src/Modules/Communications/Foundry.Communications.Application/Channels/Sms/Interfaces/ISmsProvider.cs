namespace Foundry.Communications.Application.Channels.Sms.Interfaces;

public readonly record struct SmsDeliveryResult(bool Success, string? MessageSid, string? ErrorMessage);

public interface ISmsProvider
{
    Task<SmsDeliveryResult> SendAsync(string to, string body, CancellationToken cancellationToken = default);
}
