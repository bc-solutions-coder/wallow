using Foundry.Notifications.Domain.Enums;

namespace Foundry.Notifications.Application.Channels.Email.Commands.UpdateEmailPreferences;

public sealed record UpdateEmailPreferencesCommand(
    Guid UserId,
    NotificationType NotificationType,
    bool IsEnabled);
