using Wallow.Notifications.Application.Channels.InApp.Interfaces;
using Wallow.Shared.Kernel.Results;

namespace Wallow.Notifications.Application.Channels.InApp.Commands.MarkAllNotificationsRead;

public sealed class MarkAllNotificationsReadHandler(
    INotificationRepository notificationRepository,
    TimeProvider timeProvider)
{
    public async Task<Result> Handle(
        MarkAllNotificationsReadCommand command,
        CancellationToken cancellationToken)
    {
        DateTime readAt = timeProvider.GetUtcNow().UtcDateTime;

        await notificationRepository.MarkAllAsReadAsync(
            command.UserId,
            readAt,
            cancellationToken);

        return Result.Success();
    }
}
