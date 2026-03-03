using Foundry.Communications.Domain.Channels.InApp.Entities;
using Foundry.Communications.Domain.Enums;
using Foundry.Communications.Domain.Channels.InApp.Events;
using Foundry.Shared.Kernel.Identity;

namespace Foundry.Communications.Tests.Channels.InApp.Domain.Entities;

public class NotificationCreateTests
{
    [Fact]
    public void Create_WithValidData_ReturnsNotificationInUnreadState()
    {
        TenantId tenantId = TenantId.New();
        Guid userId = Guid.NewGuid();
        NotificationType type = NotificationType.SystemAlert;
        string title = "Test Notification";
        string message = "Test message";

        Notification notification = Notification.Create(tenantId, userId, type, title, message, TimeProvider.System);

        notification.TenantId.Should().Be(tenantId);
        notification.UserId.Should().Be(userId);
        notification.Type.Should().Be(type);
        notification.Title.Should().Be(title);
        notification.Message.Should().Be(message);
        notification.IsRead.Should().BeFalse();
        notification.ReadAt.Should().BeNull();
    }

    [Fact]
    public void Create_RaisesNotificationCreatedEvent()
    {
        Guid userId = Guid.NewGuid();
        string title = "Test";

        Notification notification = Notification.Create(TenantId.New(), userId, NotificationType.SystemAlert, title, "Message", TimeProvider.System);

        notification.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<NotificationCreatedDomainEvent>()
            .Which.Should().Match<NotificationCreatedDomainEvent>(e =>
                e.UserId == userId && e.Title == title);
    }
}

public class NotificationMarkAsReadTests
{
    [Fact]
    public void MarkAsRead_ChangesIsReadToTrueAndSetsReadAt()
    {
        Notification notification = Notification.Create(TenantId.New(), Guid.NewGuid(), NotificationType.SystemAlert, "Test", "Message", TimeProvider.System);
        DateTime beforeRead = DateTime.UtcNow;

        notification.MarkAsRead(TimeProvider.System);

        notification.IsRead.Should().BeTrue();
        notification.ReadAt.Should().NotBeNull();
        notification.ReadAt.Should().BeOnOrAfter(beforeRead);
    }

    [Fact]
    public void MarkAsRead_RaisesNotificationReadEvent()
    {
        Guid userId = Guid.NewGuid();
        Notification notification = Notification.Create(TenantId.New(), userId, NotificationType.SystemAlert, "Test", "Message", TimeProvider.System);

        notification.MarkAsRead(TimeProvider.System);

        notification.DomainEvents.Should().Contain(e => e is NotificationReadDomainEvent);
    }

    [Fact]
    public void MarkAsRead_CalledTwice_UpdatesReadAt()
    {
        Notification notification = Notification.Create(TenantId.New(), Guid.NewGuid(), NotificationType.SystemAlert, "Test", "Message", TimeProvider.System);
        notification.MarkAsRead(TimeProvider.System);
        DateTime? firstReadAt = notification.ReadAt;

        Thread.Sleep(10);
        notification.MarkAsRead(TimeProvider.System);

        notification.ReadAt.Should().BeAfter(firstReadAt!.Value);
    }
}

public class NotificationOptionalPropertiesTests
{
    [Fact]
    public void Create_WithActionUrl_SetsActionUrl()
    {
        string actionUrl = "https://app.example.com/invoices/123";

        Notification notification = Notification.Create(TenantId.New(), Guid.NewGuid(), NotificationType.SystemAlert, "Invoice Ready", "Your invoice is ready", TimeProvider.System, actionUrl: actionUrl);

        notification.ActionUrl.Should().Be(actionUrl);
    }

    [Fact]
    public void Create_WithoutActionUrl_ActionUrlIsNull()
    {
        Notification notification = Notification.Create(TenantId.New(), Guid.NewGuid(), NotificationType.SystemAlert, "Test", "Message", TimeProvider.System);

        notification.ActionUrl.Should().BeNull();
    }

    [Fact]
    public void Create_WithSourceModule_SetsSourceModule()
    {
        string sourceModule = "Billing";

        Notification notification = Notification.Create(TenantId.New(), Guid.NewGuid(), NotificationType.SystemAlert, "Invoice Ready", "Your invoice is ready", TimeProvider.System, sourceModule: sourceModule);

        notification.SourceModule.Should().Be(sourceModule);
    }

    [Fact]
    public void Create_WithoutSourceModule_SourceModuleIsNull()
    {
        Notification notification = Notification.Create(TenantId.New(), Guid.NewGuid(), NotificationType.SystemAlert, "Test", "Message", TimeProvider.System);

        notification.SourceModule.Should().BeNull();
    }

    [Fact]
    public void Create_WithExpiresAt_SetsExpiresAt()
    {
        DateTime expiresAt = DateTime.UtcNow.AddDays(7);

        Notification notification = Notification.Create(TenantId.New(), Guid.NewGuid(), NotificationType.SystemAlert, "Expiring Notice", "This will expire", TimeProvider.System, expiresAt: expiresAt);

        notification.ExpiresAt.Should().Be(expiresAt);
    }

    [Fact]
    public void Create_WithoutExpiresAt_ExpiresAtIsNull()
    {
        Notification notification = Notification.Create(TenantId.New(), Guid.NewGuid(), NotificationType.SystemAlert, "Test", "Message", TimeProvider.System);

        notification.ExpiresAt.Should().BeNull();
    }

    [Fact]
    public void Create_WithAllOptionalProperties_SetsAllProperties()
    {
        string actionUrl = "https://app.example.com/settings";
        string sourceModule = "Configuration";
        DateTime expiresAt = DateTime.UtcNow.AddHours(24);

        Notification notification = Notification.Create(TenantId.New(), Guid.NewGuid(), NotificationType.SystemAlert, "Full Notification", "With all optional fields", TimeProvider.System, actionUrl: actionUrl, sourceModule: sourceModule, expiresAt: expiresAt);

        notification.ActionUrl.Should().Be(actionUrl);
        notification.SourceModule.Should().Be(sourceModule);
        notification.ExpiresAt.Should().Be(expiresAt);
    }
}

public class NotificationArchiveTests
{
    [Fact]
    public void Archive_SetsIsArchivedToTrue()
    {
        Notification notification = Notification.Create(TenantId.New(), Guid.NewGuid(), NotificationType.SystemAlert, "Test", "Message", TimeProvider.System);

        notification.Archive(TimeProvider.System);

        notification.IsArchived.Should().BeTrue();
    }

    [Fact]
    public void Create_DefaultIsArchivedIsFalse()
    {
        Notification notification = Notification.Create(TenantId.New(), Guid.NewGuid(), NotificationType.SystemAlert, "Test", "Message", TimeProvider.System);

        notification.IsArchived.Should().BeFalse();
    }

    [Fact]
    public void Archive_CalledTwice_RemainsArchived()
    {
        Notification notification = Notification.Create(TenantId.New(), Guid.NewGuid(), NotificationType.SystemAlert, "Test", "Message", TimeProvider.System);

        notification.Archive(TimeProvider.System);
        notification.Archive(TimeProvider.System);

        notification.IsArchived.Should().BeTrue();
    }
}
