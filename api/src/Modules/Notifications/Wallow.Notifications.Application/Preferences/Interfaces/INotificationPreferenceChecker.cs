using Wallow.Notifications.Domain.Preferences;
using Wallow.Shared.Kernel.Identity;

namespace Wallow.Notifications.Application.Preferences.Interfaces;

public interface INotificationPreferenceChecker
{
    Task<bool> IsChannelEnabledAsync(UserId userId, ChannelType channelType, string notificationType, CancellationToken cancellationToken = default);
}
