using Wallow.Notifications.Domain.Enums;

namespace Wallow.Notifications.Application.Channels.Email.Commands.UpdateEmailPreferences;

public sealed record UpdateEmailPreferencesCommand(
    Guid UserId,
    NotificationType NotificationType,
    bool IsEnabled);
