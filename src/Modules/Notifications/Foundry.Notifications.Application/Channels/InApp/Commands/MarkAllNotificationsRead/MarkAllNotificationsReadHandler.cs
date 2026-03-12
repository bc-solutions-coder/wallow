using Foundry.Notifications.Application.Channels.InApp.Interfaces;
using Foundry.Notifications.Domain.Channels.InApp.Entities;
using Foundry.Shared.Kernel.Results;

namespace Foundry.Notifications.Application.Channels.InApp.Commands.MarkAllNotificationsRead;

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
