using JetBrains.Annotations;

namespace Foundry.Notifications.Application.Channels.Sms.Interfaces;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public readonly record struct SmsDeliveryResult(bool Success, string? MessageSid, string? ErrorMessage);

public interface ISmsProvider
{
    Task<SmsDeliveryResult> SendAsync(string to, string body, CancellationToken cancellationToken = default);
}
