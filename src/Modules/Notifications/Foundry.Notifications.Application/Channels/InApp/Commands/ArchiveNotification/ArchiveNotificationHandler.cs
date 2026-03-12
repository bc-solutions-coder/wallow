using Foundry.Notifications.Application.Channels.InApp.Interfaces;
using Foundry.Notifications.Domain.Channels.InApp.Entities;
using Foundry.Shared.Kernel.Results;

namespace Foundry.Notifications.Application.Channels.InApp.Commands.ArchiveNotification;

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
            return Result.Failure(Error.NotFound("Notification", command.NotificationId.Value));
        }

        if (notification.TenantId != command.TenantId)
        {
            return Result.Failure(Error.Unauthorized("Unauthorized access to notification"));
        }

        if (notification.UserId != command.UserId)
        {
            return Result.Failure(Error.Unauthorized("Unauthorized access to notification"));
        }

        notification.Archive(timeProvider);
        await notificationRepository.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
