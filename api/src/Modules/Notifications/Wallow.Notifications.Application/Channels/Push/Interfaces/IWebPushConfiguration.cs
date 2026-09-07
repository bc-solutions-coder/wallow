using Wallow.Notifications.Domain.Channels.Push;

namespace Wallow.Notifications.Application.Channels.Push.Interfaces;

public sealed record WebPushPublicKey(string KeyId, string PublicKey);

public interface IWebPushConfiguration
{
    Task<WebPushPublicKey?> RotateAsync(string subject, CancellationToken cancellationToken);
    Task<bool> RetireAsync(string keyId, CancellationToken cancellationToken);
    Task<IReadOnlyList<WebPushKeyVersion>> GetVersionsAsync(CancellationToken cancellationToken);
    Task<bool> IsAvailableAsync(string? keyId, CancellationToken cancellationToken);
    Task<WebPushPublicKey?> GetCurrentKeyAsync(CancellationToken cancellationToken);
    Task<bool> CanRegisterAsync(WebPushSubscription subscription, string keyId, bool existingSubscription, CancellationToken cancellationToken);
    Task<bool> ValidateReplacementAsync(string credentials, string? previousCredentials, CancellationToken cancellationToken);
}

public sealed record WebPushKeyVersion(string KeyId, string PublicKey, bool Retired, bool Current);
