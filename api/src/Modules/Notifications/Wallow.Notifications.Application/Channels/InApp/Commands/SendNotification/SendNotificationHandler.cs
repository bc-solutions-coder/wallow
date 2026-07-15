using Wallow.Notifications.Application.Channels.InApp.DTOs;
using Wallow.Notifications.Application.Channels.InApp.Interfaces;
using Wallow.Notifications.Application.Channels.InApp.Mappings;
using Wallow.Notifications.Domain.Channels.InApp.Entities;
using Wallow.Shared.Kernel.MultiTenancy;
using Wallow.Shared.Kernel.Results;

namespace Wallow.Notifications.Application.Channels.InApp.Commands.SendNotification;

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
            timeProvider,
            actionUrl: command.ActionUrl,
            sourceModule: command.SourceModule);

        notificationRepository.Add(notification);
        await notificationRepository.SaveChangesAsync(cancellationToken);

        await notificationService.SendToUserAsync(
            command.UserId,
            command.Title,
            command.Message,
            command.Type.ToString(),
            command.ActionUrl,
            cancellationToken);

        return Result.Success(notification.ToDto());
    }
}
