using Wallow.Notifications.Application.Channels.InApp.Commands.ArchiveNotification;
using Wallow.Notifications.Application.Channels.InApp.Interfaces;
using Wallow.Notifications.Domain.Channels.InApp.Entities;
using Wallow.Notifications.Domain.Channels.InApp.Identity;
using Wallow.Notifications.Domain.Enums;
using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.Results;

namespace Wallow.Notifications.Tests.Application.Commands.InApp;

public class ArchiveNotificationHandlerTests
{
    private readonly INotificationRepository _notificationRepository = Substitute.For<INotificationRepository>();
    private readonly TimeProvider _timeProvider = Substitute.For<TimeProvider>();
    private readonly ArchiveNotificationHandler _handler;

    public ArchiveNotificationHandlerTests()
    {
        _timeProvider.GetUtcNow().Returns(DateTimeOffset.UtcNow);
        _handler = new ArchiveNotificationHandler(_notificationRepository, _timeProvider);
    }

    [Fact]
    public async Task Handle_WhenOwnerArchives_ArchivesAndSaves()
    {
        TenantId tenantId = TenantId.New();
        Guid userId = Guid.NewGuid();
        Notification notification = Notification.Create(
            tenantId, userId, NotificationType.Announcement, "Title", "Body", _timeProvider);

        _notificationRepository
            .GetByIdAsync(notification.Id, Arg.Any<CancellationToken>())
            .Returns(notification);

        ArchiveNotificationCommand command = new(notification.Id, tenantId, userId);

        Result result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        notification.IsArchived.Should().BeTrue();
        await _notificationRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenNotificationNotFound_ReturnsNotFoundFailure()
    {
        _notificationRepository
            .GetByIdAsync(Arg.Any<NotificationId>(), Arg.Any<CancellationToken>())
            .Returns((Notification?)null);

        ArchiveNotificationCommand command = new(NotificationId.New(), TenantId.New(), Guid.NewGuid());

        Result result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WhenDifferentTenant_ReturnsUnauthorizedFailure()
    {
        TenantId ownerTenant = TenantId.New();
        TenantId differentTenant = TenantId.New();
        Guid userId = Guid.NewGuid();

        Notification notification = Notification.Create(
            ownerTenant, userId, NotificationType.SystemAlert, "Title", "Body", _timeProvider);

        _notificationRepository
            .GetByIdAsync(notification.Id, Arg.Any<CancellationToken>())
            .Returns(notification);

        ArchiveNotificationCommand command = new(notification.Id, differentTenant, userId);

        Result result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WhenDifferentUser_ReturnsUnauthorizedFailure()
    {
        TenantId tenantId = TenantId.New();
        Guid ownerId = Guid.NewGuid();
        Guid differentUserId = Guid.NewGuid();

        Notification notification = Notification.Create(
            tenantId, ownerId, NotificationType.TaskComment, "Title", "Body", _timeProvider);

        _notificationRepository
            .GetByIdAsync(notification.Id, Arg.Any<CancellationToken>())
            .Returns(notification);

        ArchiveNotificationCommand command = new(notification.Id, tenantId, differentUserId);

        Result result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
    }
}
