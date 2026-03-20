using Wallow.Notifications.Application.Channels.InApp.Interfaces;
using Wallow.Notifications.Domain.Channels.InApp.Entities;
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
        IReadOnlyList<Notification> unreadNotifications = await notificationRepository.GetUnreadByUserIdAsync(
            command.UserId,
            cancellationToken);

        foreach (Notification notification in unreadNotifications)
        {
            notification.MarkAsRead(timeProvider);
        }

        await notificationRepository.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
