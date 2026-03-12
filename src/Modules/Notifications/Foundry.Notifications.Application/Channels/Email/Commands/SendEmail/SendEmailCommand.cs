using Foundry.Shared.Kernel.Identity;

namespace Foundry.Notifications.Application.Channels.Email.Commands.SendEmail;

public sealed record SendEmailCommand(
    string To,
    string? From,
    string Subject,
    string Body,
    UserId? UserId = null,
    string? NotificationType = null);
