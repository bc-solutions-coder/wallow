namespace Wallow.Notifications.Application.Channels.InApp.DTOs;

public sealed record NotificationDto(
    Guid Id,
    Guid UserId,
    string Type,
    string Title,
    string Message,
    bool IsRead,
    DateTime? ReadAt,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
