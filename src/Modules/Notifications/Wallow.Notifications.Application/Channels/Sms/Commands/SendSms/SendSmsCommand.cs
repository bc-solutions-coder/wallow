using Wallow.Shared.Kernel.Identity;

namespace Wallow.Notifications.Application.Channels.Sms.Commands.SendSms;

public sealed record SendSmsCommand(
    string To,
    string Body,
    string? From = null,
    UserId? UserId = null,
    string? NotificationType = null);
