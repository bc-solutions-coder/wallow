using Foundry.Notifications.Domain.Channels.InApp.Entities;
using Foundry.Notifications.Domain.Channels.InApp.Events;
using Foundry.Notifications.Domain.Enums;
using Foundry.Shared.Kernel.Identity;

namespace Foundry.Notifications.Tests.Domain.Entities;

public class NotificationCreateTests
{
    [Fact]
    public void Create_WithValidData_ReturnsNotificationWithExpectedProperties()
    {
        TenantId tenantId = TenantId.New();
        Guid userId = Guid.NewGuid();
        NotificationType type = NotificationType.TaskAssigned;
        string title = "New Task";
        string message = "You have been assigned a task.";
        string actionUrl = "/tasks/123";
        string sourceModule = "Identity";
        DateTime expiresAt = DateTime.UtcNow.AddDays(7);

        Notification notification = Notification.Create(
            tenantId, userId, type, title, message, TimeProvider.System,
            actionUrl, sourceModule, expiresAt);

        notification.TenantId.Should().Be(tenantId);
        notification.UserId.Should().Be(userId);
        notification.Type.Should().Be(type);
        notification.Title.Should().Be(title);
        notification.Message.Should().Be(message);
        notification.ActionUrl.Should().Be(actionUrl);
        notification.SourceModule.Should().Be(sourceModule);
        notification.ExpiresAt.Should().Be(expiresAt);
        notification.IsRead.Should().BeFalse();
        notification.IsArchived.Should().BeFalse();
    }

    [Fact]
    public void Create_WithOptionalNulls_SetsNullableFieldsToNull()
    {
        Notification notification = Notification.Create(
            TenantId.New(), Guid.NewGuid(), NotificationType.SystemAlert,
            "Alert", "System alert", TimeProvider.System);

        notification.ActionUrl.Should().BeNull();
        notification.SourceModule.Should().BeNull();
        notification.ExpiresAt.Should().BeNull();
    }

    [Fact]
    public void Create_RaisesNotificationCreatedDomainEvent()
    {
        TenantId tenantId = TenantId.New();
        Guid userId = Guid.NewGuid();
        string title = "Event Test";

        Notification notification = Notification.Create(
            tenantId, userId, NotificationType.Mention, title, "Body", TimeProvider.System);

        notification.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<NotificationCreatedDomainEvent>()
            .Which.Should().Match<NotificationCreatedDomainEvent>(e =>
                e.NotificationId == notification.Id.Value &&
                e.UserId == userId &&
                e.Title == title &&
                e.Type == NotificationType.Mention.ToString());
    }

    [Fact]
    public void Create_SetsCreatedTimestamp()
    {
        DateTime before = DateTime.UtcNow;

        Notification notification = Notification.Create(
            TenantId.New(), Guid.NewGuid(), NotificationType.SystemAlert,
            "Test", "Message", TimeProvider.System);

        notification.CreatedAt.Should().BeOnOrAfter(before);
        notification.CreatedAt.Should().BeOnOrBefore(DateTime.UtcNow);
    }

    [Fact]
    public void MarkAsRead_SetsIsReadAndReadAt()
    {
        Notification notification = Notification.Create(
            TenantId.New(), Guid.NewGuid(), NotificationType.TaskCompleted,
            "Done", "Task completed", TimeProvider.System);

        DateTime before = DateTime.UtcNow;
        notification.MarkAsRead(TimeProvider.System);

        notification.IsRead.Should().BeTrue();
        notification.ReadAt.Should().NotBeNull();
        notification.ReadAt!.Value.Should().BeOnOrAfter(before);
    }

    [Fact]
    public void MarkAsRead_RaisesNotificationReadDomainEvent()
    {
        Guid userId = Guid.NewGuid();
        Notification notification = Notification.Create(
            TenantId.New(), userId, NotificationType.TaskComment,
            "Comment", "New comment", TimeProvider.System);
        notification.ClearDomainEvents();

        notification.MarkAsRead(TimeProvider.System);

        notification.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<NotificationReadDomainEvent>()
            .Which.Should().Match<NotificationReadDomainEvent>(e =>
                e.NotificationId == notification.Id.Value &&
                e.UserId == userId);
    }

    [Fact]
    public void Archive_SetsIsArchivedToTrue()
    {
        Notification notification = Notification.Create(
            TenantId.New(), Guid.NewGuid(), NotificationType.Announcement,
            "Archived", "To be archived", TimeProvider.System);

        notification.Archive(TimeProvider.System);

        notification.IsArchived.Should().BeTrue();
    }
}
