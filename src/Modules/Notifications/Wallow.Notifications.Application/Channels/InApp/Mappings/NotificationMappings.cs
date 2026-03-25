using Wallow.Notifications.Application.Channels.InApp.DTOs;
using Wallow.Notifications.Domain.Channels.InApp.Entities;

namespace Wallow.Notifications.Application.Channels.InApp.Mappings;

public static class NotificationMappings
{
    public static NotificationDto ToDto(this Notification notification)
    {
        return new NotificationDto(
            notification.Id.Value,
            notification.UserId,
            notification.Type.ToString(),
            notification.Title,
            notification.Message,
            notification.IsRead,
            notification.ReadAt,
            notification.ActionUrl,
            notification.CreatedAt,
            notification.UpdatedAt);
    }
}
