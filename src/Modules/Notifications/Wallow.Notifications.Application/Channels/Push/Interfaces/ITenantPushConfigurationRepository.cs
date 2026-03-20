using Wallow.Notifications.Domain.Channels.Push.Entities;
using Wallow.Notifications.Domain.Channels.Push.Enums;

namespace Wallow.Notifications.Application.Channels.Push.Interfaces;

public interface ITenantPushConfigurationRepository
{
    Task<TenantPushConfiguration?> GetAsync(CancellationToken cancellationToken = default);
    Task<TenantPushConfiguration?> GetByPlatformAsync(PushPlatform platform, CancellationToken cancellationToken = default);
    Task UpsertAsync(TenantPushConfiguration configuration, CancellationToken cancellationToken = default);
    Task DeleteByPlatformAsync(PushPlatform platform, CancellationToken cancellationToken = default);
}
