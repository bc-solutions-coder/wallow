using Wallow.Notifications.Domain.Channels.Push.Entities;

namespace Wallow.Notifications.Application.Channels.Push.Interfaces;

public readonly record struct PushDeliveryResult(bool Success, string? ErrorMessage, bool SubscriptionExpired = false, bool Retryable = false, TimeSpan? RetryAfter = null);

public interface IPushProvider
{
    Task<PushDeliveryResult> SendAsync(PushMessage message, string deviceToken, CancellationToken cancellationToken = default);
}
