namespace Foundry.Notifications.Api.Contracts.InApp.Responses;

public sealed record PagedNotificationResponse(
    IReadOnlyList<NotificationResponse> Items,
    int TotalCount,
    int PageNumber,
    int PageSize,
    int TotalPages,
    bool HasPreviousPage,
    bool HasNextPage);
