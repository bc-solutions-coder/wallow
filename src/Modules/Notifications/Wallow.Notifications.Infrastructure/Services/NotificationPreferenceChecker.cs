using Wallow.Notifications.Application.Preferences.Interfaces;
using Wallow.Notifications.Domain.Preferences;
using Wallow.Notifications.Infrastructure.Persistence;
using Wallow.Shared.Kernel.Identity;
using Microsoft.EntityFrameworkCore;

namespace Wallow.Notifications.Infrastructure.Services;

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
