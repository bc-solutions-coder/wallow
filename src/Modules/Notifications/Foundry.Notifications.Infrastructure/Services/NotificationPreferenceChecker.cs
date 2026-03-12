using Foundry.Notifications.Application.Preferences.Interfaces;
using Foundry.Notifications.Domain.Preferences;
using Foundry.Notifications.Infrastructure.Persistence;
using Foundry.Shared.Kernel.Identity;
using Microsoft.EntityFrameworkCore;

namespace Foundry.Notifications.Infrastructure.Services;

internal sealed class NotificationPreferenceChecker(NotificationsDbContext dbContext) : INotificationPreferenceChecker
{
    public async Task<bool> IsChannelEnabledAsync(
        UserId userId,
        ChannelType channelType,
        string notificationType,
        CancellationToken cancellationToken = default)
    {
        Guid userGuid = userId.Value;

        // Check global kill-switch: wildcard "*" for this channel
        bool? globalEnabled = await dbContext.ChannelPreferences
            .Where(p => p.UserId == userGuid
                        && p.ChannelType == channelType
                        && p.NotificationType == "*")
            .Select(p => (bool?)p.IsEnabled)
            .FirstOrDefaultAsync(cancellationToken);

        if (globalEnabled == false)
        {
            return false;
        }

        // Check specific notification type preference
        bool? specificEnabled = await dbContext.ChannelPreferences
            .Where(p => p.UserId == userGuid
                        && p.ChannelType == channelType
                        && p.NotificationType == notificationType)
            .Select(p => (bool?)p.IsEnabled)
            .FirstOrDefaultAsync(cancellationToken);

        if (specificEnabled == false)
        {
            return false;
        }

        // Opt-out model: enabled by default
        return true;
    }
}
