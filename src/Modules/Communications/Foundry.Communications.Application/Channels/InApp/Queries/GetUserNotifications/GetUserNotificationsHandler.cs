using Foundry.Communications.Application.Channels.InApp.DTOs;
using Foundry.Communications.Application.Channels.InApp.Interfaces;
using Foundry.Communications.Application.Channels.InApp.Mappings;
using Foundry.Shared.Kernel.Pagination;
using Foundry.Shared.Kernel.Results;

namespace Foundry.Communications.Application.Channels.InApp.Queries.GetUserNotifications;

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
