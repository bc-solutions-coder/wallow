namespace Foundry.Notifications.Application.Channels.InApp.Queries.GetUserNotifications;

public sealed record GetUserNotificationsQuery(Guid UserId, int PageNumber = 1, int PageSize = 20);
