using Foundry.Notifications.Domain.Preferences;
using Foundry.Shared.Kernel.Identity;

namespace Foundry.Notifications.Application.Preferences.Interfaces;

public interface INotificationPreferenceChecker
{
    Task<bool> IsChannelEnabledAsync(UserId userId, ChannelType channelType, string notificationType, CancellationToken cancellationToken = default);
}
