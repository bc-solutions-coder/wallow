using Wallow.Notifications.Application.Channels.InApp.Interfaces;
using Wallow.Shared.Kernel.Results;

namespace Wallow.Notifications.Application.Channels.InApp.Queries.GetUnreadCount;

public sealed class GetUnreadCountHandler(INotificationRepository notificationRepository)
{
    public async Task<Result<int>> Handle(
        GetUnreadCountQuery query,
        CancellationToken cancellationToken)
    {
        int count = await notificationRepository.GetUnreadCountAsync(
            query.UserId,
            cancellationToken);

        return Result.Success(count);
    }
}
