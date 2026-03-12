using Foundry.Notifications.Application.Channels.InApp.DTOs;
using Foundry.Notifications.Application.Channels.InApp.Interfaces;
using Foundry.Notifications.Application.Channels.InApp.Mappings;
using Foundry.Notifications.Domain.Channels.InApp.Entities;
using Foundry.Shared.Kernel.MultiTenancy;
using Foundry.Shared.Kernel.Results;

namespace Foundry.Notifications.Application.Channels.InApp.Commands.SendNotification;

public sealed class SendNotificationHandler(
    INotificationRepository notificationRepository,
    INotificationService notificationService,
    ITenantContext tenantContext,
    TimeProvider timeProvider)
{
    public async Task<Result<NotificationDto>> Handle(
        SendNotificationCommand command,
        CancellationToken cancellationToken)
    {
        Notification notification = Notification.Create(
            tenantContext.TenantId,
            command.UserId,
            command.Type,
            command.Title,
            command.Message,
            timeProvider);

        notificationRepository.Add(notification);
        await notificationRepository.SaveChangesAsync(cancellationToken);

        await notificationService.SendToUserAsync(
            command.UserId,
            command.Title,
            command.Message,
            command.Type.ToString(),
            cancellationToken);

        return Result.Success(notification.ToDto());
    }
}
