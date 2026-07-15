using Microsoft.EntityFrameworkCore;
using Wallow.Notifications.Application.Preferences.Interfaces;
using Wallow.Notifications.Domain.Preferences;
using Wallow.Notifications.Domain.Preferences.Entities;

namespace Wallow.Notifications.Infrastructure.Persistence.Repositories;

public sealed class ChannelPreferenceRepository(NotificationsDbContext context) : IChannelPreferenceRepository
{

    public Task<ChannelPreference?> GetByUserChannelAndNotificationTypeAsync(Guid userId, ChannelType channelType, string notificationType, CancellationToken cancellationToken = default)
    {
        return context.ChannelPreferences
            .AsTracking()
            .FirstOrDefaultAsync(
                cp => cp.UserId == userId && cp.ChannelType == channelType && cp.NotificationType == notificationType,
                cancellationToken);
    }

    public async Task<IReadOnlyList<ChannelPreference>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await context.ChannelPreferences
            .Where(cp => cp.UserId == userId)
            .ToListAsync(cancellationToken);
    }

    public void Add(ChannelPreference preference)
    {
        context.ChannelPreferences.Add(preference);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await context.SaveChangesAsync(cancellationToken);
    }
}
