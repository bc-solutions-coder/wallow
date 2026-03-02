using Foundry.Communications.Domain.Channels.InApp.Entities;
using Foundry.Communications.Domain.Channels.InApp.Enums;
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

        Notification notification = Notification.Create(tenantId, userId, type, title, message);

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

        Notification notification = Notification.Create(
            TenantId.New(),
            userId,
            NotificationType.SystemAlert,
            title,
            "Message");

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
        Notification notification = Notification.Create(
            TenantId.New(),
            Guid.NewGuid(),
            NotificationType.SystemAlert,
            "Test",
            "Message");
        DateTime beforeRead = DateTime.UtcNow;

        notification.MarkAsRead();

        notification.IsRead.Should().BeTrue();
        notification.ReadAt.Should().NotBeNull();
        notification.ReadAt.Should().BeOnOrAfter(beforeRead);
    }

    [Fact]
    public void MarkAsRead_RaisesNotificationReadEvent()
    {
        Guid userId = Guid.NewGuid();
        Notification notification = Notification.Create(
            TenantId.New(),
            userId,
            NotificationType.SystemAlert,
            "Test",
            "Message");

        notification.MarkAsRead();

        notification.DomainEvents.Should().Contain(e => e is NotificationReadDomainEvent);
    }

    [Fact]
    public void MarkAsRead_CalledTwice_UpdatesReadAt()
    {
        Notification notification = Notification.Create(
            TenantId.New(),
            Guid.NewGuid(),
            NotificationType.SystemAlert,
            "Test",
            "Message");
        notification.MarkAsRead();
        DateTime? firstReadAt = notification.ReadAt;

        Thread.Sleep(10);
        notification.MarkAsRead();

        notification.ReadAt.Should().BeAfter(firstReadAt!.Value);
    }
}

public class NotificationOptionalPropertiesTests
{
    [Fact]
    public void Create_WithActionUrl_SetsActionUrl()
    {
        string actionUrl = "https://app.example.com/invoices/123";

        Notification notification = Notification.Create(
            TenantId.New(),
            Guid.NewGuid(),
            NotificationType.SystemAlert,
            "Invoice Ready",
            "Your invoice is ready",
            actionUrl: actionUrl);

        notification.ActionUrl.Should().Be(actionUrl);
    }

    [Fact]
    public void Create_WithoutActionUrl_ActionUrlIsNull()
    {
        Notification notification = Notification.Create(
            TenantId.New(),
            Guid.NewGuid(),
            NotificationType.SystemAlert,
            "Test",
            "Message");

        notification.ActionUrl.Should().BeNull();
    }

    [Fact]
    public void Create_WithSourceModule_SetsSourceModule()
    {
        string sourceModule = "Billing";

        Notification notification = Notification.Create(
            TenantId.New(),
            Guid.NewGuid(),
            NotificationType.SystemAlert,
            "Invoice Ready",
            "Your invoice is ready",
            sourceModule: sourceModule);

        notification.SourceModule.Should().Be(sourceModule);
    }

    [Fact]
    public void Create_WithoutSourceModule_SourceModuleIsNull()
    {
        Notification notification = Notification.Create(
            TenantId.New(),
            Guid.NewGuid(),
            NotificationType.SystemAlert,
            "Test",
            "Message");

        notification.SourceModule.Should().BeNull();
    }

    [Fact]
    public void Create_WithExpiresAt_SetsExpiresAt()
    {
        DateTime expiresAt = DateTime.UtcNow.AddDays(7);

        Notification notification = Notification.Create(
            TenantId.New(),
            Guid.NewGuid(),
            NotificationType.SystemAlert,
            "Expiring Notice",
            "This will expire",
            expiresAt: expiresAt);

        notification.ExpiresAt.Should().Be(expiresAt);
    }

    [Fact]
    public void Create_WithoutExpiresAt_ExpiresAtIsNull()
    {
        Notification notification = Notification.Create(
            TenantId.New(),
            Guid.NewGuid(),
            NotificationType.SystemAlert,
            "Test",
            "Message");

        notification.ExpiresAt.Should().BeNull();
    }

    [Fact]
    public void IsExpired_WhenExpiresAtInPast_ReturnsTrue()
    {
        Notification notification = Notification.Create(
            TenantId.New(),
            Guid.NewGuid(),
            NotificationType.SystemAlert,
            "Expired Notice",
            "This has expired",
            expiresAt: DateTime.UtcNow.AddMinutes(-1));

        notification.IsExpired.Should().BeTrue();
    }

    [Fact]
    public void IsExpired_WhenExpiresAtInFuture_ReturnsFalse()
    {
        Notification notification = Notification.Create(
            TenantId.New(),
            Guid.NewGuid(),
            NotificationType.SystemAlert,
            "Future Notice",
            "Not expired yet",
            expiresAt: DateTime.UtcNow.AddDays(1));

        notification.IsExpired.Should().BeFalse();
    }

    [Fact]
    public void Create_WithAllOptionalProperties_SetsAllProperties()
    {
        string actionUrl = "https://app.example.com/settings";
        string sourceModule = "Configuration";
        DateTime expiresAt = DateTime.UtcNow.AddHours(24);

        Notification notification = Notification.Create(
            TenantId.New(),
            Guid.NewGuid(),
            NotificationType.SystemAlert,
            "Full Notification",
            "With all optional fields",
            actionUrl: actionUrl,
            sourceModule: sourceModule,
            expiresAt: expiresAt);

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
        Notification notification = Notification.Create(
            TenantId.New(),
            Guid.NewGuid(),
            NotificationType.SystemAlert,
            "Test",
            "Message");

        notification.Archive();

        notification.IsArchived.Should().BeTrue();
    }

    [Fact]
    public void Create_DefaultIsArchivedIsFalse()
    {
        Notification notification = Notification.Create(
            TenantId.New(),
            Guid.NewGuid(),
            NotificationType.SystemAlert,
            "Test",
            "Message");

        notification.IsArchived.Should().BeFalse();
    }

    [Fact]
    public void Archive_CalledTwice_RemainsArchived()
    {
        Notification notification = Notification.Create(
            TenantId.New(),
            Guid.NewGuid(),
            NotificationType.SystemAlert,
            "Test",
            "Message");

        notification.Archive();
        notification.Archive();

        notification.IsArchived.Should().BeTrue();
    }
}
