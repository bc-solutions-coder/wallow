using Wallow.Notifications.Domain.Channels.Push.Entities;

namespace Wallow.Notifications.Application.Channels.Push.Interfaces;

public readonly record struct PushDeliveryResult(bool Success, string? ErrorMessage);

public interface IPushProvider
{
    Task<PushDeliveryResult> SendAsync(PushMessage message, string deviceToken, CancellationToken cancellationToken = default);
}
