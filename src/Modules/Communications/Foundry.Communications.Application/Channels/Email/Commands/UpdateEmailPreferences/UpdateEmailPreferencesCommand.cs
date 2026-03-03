using Foundry.Communications.Domain.Enums;

namespace Foundry.Communications.Application.Channels.Email.Commands.UpdateEmailPreferences;

public sealed record UpdateEmailPreferencesCommand(
    Guid UserId,
    NotificationType NotificationType,
    bool IsEnabled);
