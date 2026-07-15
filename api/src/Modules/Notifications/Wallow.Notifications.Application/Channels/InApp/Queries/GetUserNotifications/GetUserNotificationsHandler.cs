using Wallow.Notifications.Application.Channels.InApp.DTOs;
using Wallow.Notifications.Application.Channels.InApp.Interfaces;
using Wallow.Notifications.Application.Channels.InApp.Mappings;
using Wallow.Shared.Kernel.Pagination;
using Wallow.Shared.Kernel.Results;

namespace Wallow.Notifications.Application.Channels.InApp.Queries.GetUserNotifications;

public sealed class GetUserNotificationsHandler(
    INotificationRepository notificationRepository)
{
    public async Task<Result<PagedResult<NotificationDto>>> Handle(
        GetUserNotificationsQuery query,
        CancellationToken cancellationToken)
    {
        PagedResult<Domain.Channels.InApp.Entities.Notification> pagedNotifications =
            await notificationRepository.GetByUserIdPagedAsync(
                query.UserId,
                query.PageNumber,
                query.PageSize,
                cancellationToken);

        List<NotificationDto> dtos = pagedNotifications.Items
            .Select(n => n.ToDto())
            .ToList();

        PagedResult<NotificationDto> pagedResult = new(
            dtos,
            pagedNotifications.TotalCount,
            query.PageNumber,
            query.PageSize);

        return Result.Success(pagedResult);
    }
}
