using Foundry.Communications.Application.Channels.InApp.DTOs;
using Foundry.Communications.Application.Channels.InApp.Mappings;
using Foundry.Communications.Domain.Channels.InApp.Entities;
using Foundry.Communications.Domain.Enums;
using Foundry.Shared.Kernel.Identity;

namespace Foundry.Communications.Tests.Application.Channels.InApp.Mappings;

public class NotificationMappingsTests
{
    [Fact]
    public void ToDto_MapsAllFields()
    {
        Guid userId = Guid.NewGuid();
        TenantId tenantId = TenantId.Create(Guid.NewGuid());

        Notification notification = Notification.Create(tenantId, userId, NotificationType.TaskAssigned, "Task Title", "Task Message", TimeProvider.System);

        NotificationDto dto = notification.ToDto();

        dto.Id.Should().Be(notification.Id.Value);
        dto.UserId.Should().Be(userId);
        dto.Type.Should().Be(nameof(NotificationType.TaskAssigned));
        dto.Title.Should().Be("Task Title");
        dto.Message.Should().Be("Task Message");
        dto.IsRead.Should().BeFalse();
        dto.ReadAt.Should().BeNull();
    }

    [Fact]
    public void ToDto_WhenMarkedAsRead_ReflectsReadState()
    {
        Notification notification = Notification.Create(
            TenantId.Create(Guid.NewGuid()),
            Guid.NewGuid(),
            NotificationType.SystemAlert,
            "Title",
            "Message", TimeProvider.System);

        notification.MarkAsRead(TimeProvider.System);

        NotificationDto dto = notification.ToDto();

        dto.IsRead.Should().BeTrue();
        dto.ReadAt.Should().NotBeNull();
    }
}
