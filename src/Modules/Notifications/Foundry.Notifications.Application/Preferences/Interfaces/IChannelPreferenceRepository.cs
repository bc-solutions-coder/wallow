using Foundry.Notifications.Domain.Preferences;
using Foundry.Notifications.Domain.Preferences.Entities;

namespace Foundry.Notifications.Application.Preferences.Interfaces;

public interface IChannelPreferenceRepository
{
    Task<ChannelPreference?> GetByUserChannelAndNotificationTypeAsync(Guid userId, ChannelType channelType, string notificationType, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ChannelPreference>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    void Add(ChannelPreference preference);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
