using Wallow.Notifications.Application.Channels.InApp.Interfaces;
using Wallow.Notifications.Domain.Channels.InApp.Entities;
using Wallow.Notifications.Domain.Errors;
using Wallow.Shared.Kernel.Results;

namespace Wallow.Notifications.Application.Channels.InApp.Commands.ArchiveNotification;

public sealed class ArchiveNotificationHandler(
    INotificationRepository notificationRepository,
    TimeProvider timeProvider)
{
    public async Task<Result> Handle(
        ArchiveNotificationCommand command,
        CancellationToken cancellationToken)
    {
        Notification? notification = await notificationRepository.GetByIdAsync(
            command.NotificationId,
            cancellationToken);

        if (notification is null)
        {
            return Result.Failure(NotificationsErrors.NotificationNotFound);
        }

        if (notification.UserId != command.UserId)
        {
            return Result.Failure(NotificationsErrors.NotificationAccessDenied);
        }

        notification.Archive(timeProvider);
        await notificationRepository.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
