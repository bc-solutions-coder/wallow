using Foundry.Communications.Application.Preferences.Interfaces;
using Foundry.Communications.Domain.Preferences;
using Foundry.Communications.Domain.Preferences.Entities;
using Microsoft.EntityFrameworkCore;

namespace Foundry.Communications.Infrastructure.Persistence.Repositories;

public sealed class ChannelPreferenceRepository(CommunicationsDbContext context) : IChannelPreferenceRepository
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
