using Foundry.Communications.Application.Channels.InApp.Interfaces;
using Foundry.Communications.Domain.Channels.InApp.Entities;
using Foundry.Communications.Domain.Channels.InApp.Identity;
using Foundry.Shared.Kernel.Results;

namespace Foundry.Communications.Application.Channels.InApp.Commands.MarkNotificationRead;

public sealed class MarkNotificationReadHandler(
    INotificationRepository notificationRepository,
    TimeProvider timeProvider)
{
    public async Task<Result> Handle(
        MarkNotificationReadCommand command,
        CancellationToken cancellationToken)
    {
        Notification? notification = await notificationRepository.GetByIdAsync(
            NotificationId.Create(command.NotificationId),
            cancellationToken);

        if (notification is null)
        {
            return Result.Failure(Error.NotFound("Notification", command.NotificationId));
        }

        if (notification.UserId != command.UserId)
        {
            return Result.Failure(Error.Unauthorized("Unauthorized access to notification"));
        }

        notification.MarkAsRead(timeProvider);
        await notificationRepository.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
