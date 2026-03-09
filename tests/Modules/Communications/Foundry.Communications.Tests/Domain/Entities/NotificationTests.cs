using Foundry.Communications.Domain.Channels.InApp.Entities;
using Foundry.Communications.Domain.Enums;
using Foundry.Communications.Domain.Channels.InApp.Events;
using Foundry.Shared.Kernel.Identity;

namespace Foundry.Communications.Tests.Domain.Entities;

public class NotificationCreateTests
{
    [Fact]
    public void Create_WithValidData_ReturnsUnreadNotification()
    {
        TenantId tenantId = TenantId.New();
        Guid userId = Guid.NewGuid();

        Notification notification = Notification.Create(tenantId, userId, NotificationType.TaskAssigned, "Task assigned", "You have a new task", TimeProvider.System);

        notification.TenantId.Should().Be(tenantId);
        notification.UserId.Should().Be(userId);
        notification.Type.Should().Be(NotificationType.TaskAssigned);
        notification.Title.Should().Be("Task assigned");
        notification.Message.Should().Be("You have a new task");
        notification.IsRead.Should().BeFalse();
        notification.ReadAt.Should().BeNull();
    }

    [Theory]
    [InlineData(NotificationType.TaskAssigned)]
    [InlineData(NotificationType.TaskCompleted)]
    [InlineData(NotificationType.TaskComment)]
    [InlineData(NotificationType.SystemAlert)]
    [InlineData(NotificationType.BillingInvoice)]
    [InlineData(NotificationType.Mention)]
    [InlineData(NotificationType.Announcement)]
    public void Create_WithDifferentTypes_SetsTypeCorrectly(NotificationType type)
    {
        Notification notification = Notification.Create(TenantId.New(), Guid.NewGuid(), type, "Title", "Message", TimeProvider.System);

        notification.Type.Should().Be(type);
    }

    [Fact]
    public void Create_RaisesNotificationCreatedDomainEvent()
    {
        Guid userId = Guid.NewGuid();

        Notification notification = Notification.Create(TenantId.New(), userId, NotificationType.SystemAlert, "Alert", "System alert message", TimeProvider.System);

        notification.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<NotificationCreatedDomainEvent>()
            .Which.UserId.Should().Be(userId);
    }

    [Fact]
    public void Create_RaisesEventWithCorrectTitle()
    {
        Notification notification = Notification.Create(TenantId.New(), Guid.NewGuid(), NotificationType.Mention, "You were mentioned", "Body", TimeProvider.System);

        NotificationCreatedDomainEvent domainEvent = notification.DomainEvents
            .OfType<NotificationCreatedDomainEvent>().Single();

        domainEvent.Title.Should().Be("You were mentioned");
        domainEvent.Type.Should().Be(nameof(NotificationType.Mention));
    }
}

public class NotificationMarkAsReadTests
{
    [Fact]
    public void MarkAsRead_UnreadNotification_SetsIsReadToTrue()
    {
        Notification notification = Notification.Create(TenantId.New(), Guid.NewGuid(), NotificationType.TaskAssigned, "Title", "Message", TimeProvider.System);
        notification.ClearDomainEvents();

        notification.MarkAsRead(TimeProvider.System);

        notification.IsRead.Should().BeTrue();
    }

    [Fact]
    public void MarkAsRead_UnreadNotification_SetsReadAt()
    {
        Notification notification = Notification.Create(TenantId.New(), Guid.NewGuid(), NotificationType.TaskAssigned, "Title", "Message", TimeProvider.System);
        notification.ClearDomainEvents();
        DateTime beforeRead = DateTime.UtcNow;

        notification.MarkAsRead(TimeProvider.System);

        notification.ReadAt.Should().NotBeNull();
        notification.ReadAt.Should().BeOnOrAfter(beforeRead);
    }

    [Fact]
    public void MarkAsRead_RaisesNotificationReadDomainEvent()
    {
        Guid userId = Guid.NewGuid();
        Notification notification = Notification.Create(TenantId.New(), userId, NotificationType.TaskCompleted, "Title", "Message", TimeProvider.System);
        notification.ClearDomainEvents();

        notification.MarkAsRead(TimeProvider.System);

        notification.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<NotificationReadDomainEvent>()
            .Which.UserId.Should().Be(userId);
    }

    [Fact]
    public void MarkAsRead_CalledTwice_UpdatesReadAtToLatest()
    {
        Notification notification = Notification.Create(TenantId.New(), Guid.NewGuid(), NotificationType.SystemAlert, "Title", "Message", TimeProvider.System);
        notification.ClearDomainEvents();

        notification.MarkAsRead(TimeProvider.System);
        DateTime? firstReadAt = notification.ReadAt;

        notification.MarkAsRead(TimeProvider.System);

        notification.ReadAt.Should().BeOnOrAfter(firstReadAt!.Value);
        notification.IsRead.Should().BeTrue();
    }
}

public class NotificationOptionalPropertiesTests
{
    [Fact]
    public void Create_WithOptionalProperties_SetsAllPropertiesCorrectly()
    {
        TenantId tenantId = TenantId.New();
        Guid userId = Guid.NewGuid();
        DateTime expiresAt = DateTime.UtcNow.AddDays(7);

        Notification notification = Notification.Create(tenantId, userId, NotificationType.SystemAlert, "Title", "Message", TimeProvider.System, actionUrl: "https://example.com/action", sourceModule: "Billing", expiresAt: expiresAt);

        notification.ActionUrl.Should().Be("https://example.com/action");
        notification.SourceModule.Should().Be("Billing");
        notification.ExpiresAt.Should().Be(expiresAt);
        notification.IsArchived.Should().BeFalse();
    }

    [Fact]
    public void Create_WithoutOptionalProperties_DefaultsToNull()
    {
        Notification notification = Notification.Create(TenantId.New(), Guid.NewGuid(), NotificationType.TaskAssigned, "Title", "Message", TimeProvider.System);

        notification.ActionUrl.Should().BeNull();
        notification.SourceModule.Should().BeNull();
        notification.ExpiresAt.Should().BeNull();
        notification.IsArchived.Should().BeFalse();
    }
}

public class NotificationArchiveTests
{
    [Fact]
    public void Archive_SetsIsArchivedToTrue()
    {
        Notification notification = Notification.Create(TenantId.New(), Guid.NewGuid(), NotificationType.SystemAlert, "Title", "Message", TimeProvider.System);

        notification.Archive(TimeProvider.System);

        notification.IsArchived.Should().BeTrue();
    }

    [Fact]
    public void Archive_CalledTwice_RemainsArchived()
    {
        Notification notification = Notification.Create(TenantId.New(), Guid.NewGuid(), NotificationType.SystemAlert, "Title", "Message", TimeProvider.System);

        notification.Archive(TimeProvider.System);
        notification.Archive(TimeProvider.System);

        notification.IsArchived.Should().BeTrue();
    }
}

