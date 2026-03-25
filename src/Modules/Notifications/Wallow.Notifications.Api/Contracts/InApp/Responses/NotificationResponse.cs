namespace Wallow.Notifications.Api.Contracts.InApp.Responses;

public sealed record NotificationResponse(
    Guid Id,
    Guid UserId,
    string Type,
    string Title,
    string Message,
    bool IsRead,
    DateTime? ReadAt,
    string? ActionUrl,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
