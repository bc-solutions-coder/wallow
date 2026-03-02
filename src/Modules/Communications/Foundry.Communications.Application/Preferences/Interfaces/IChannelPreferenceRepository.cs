using Foundry.Communications.Domain.Preferences;
using Foundry.Communications.Domain.Preferences.Entities;
using Foundry.Communications.Domain.Preferences.Identity;

namespace Foundry.Communications.Application.Preferences.Interfaces;

public interface IChannelPreferenceRepository
{
    Task<ChannelPreference?> GetByIdAsync(ChannelPreferenceId id, CancellationToken cancellationToken = default);
    Task<ChannelPreference?> GetByUserAndChannelAsync(Guid userId, ChannelType channelType, CancellationToken cancellationToken = default);
    Task<ChannelPreference?> GetByUserChannelAndNotificationTypeAsync(Guid userId, ChannelType channelType, string notificationType, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ChannelPreference>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    void Add(ChannelPreference preference);
    void Update(ChannelPreference preference);
    void Delete(ChannelPreference preference);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
