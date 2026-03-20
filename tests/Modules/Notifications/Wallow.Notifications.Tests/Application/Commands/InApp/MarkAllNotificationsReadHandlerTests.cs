using Wallow.Notifications.Application.Channels.InApp.Commands.MarkAllNotificationsRead;
using Wallow.Notifications.Application.Channels.InApp.Interfaces;
using Wallow.Notifications.Domain.Channels.InApp.Entities;
using Wallow.Notifications.Domain.Enums;
using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.Results;

namespace Wallow.Notifications.Tests.Application.Commands.InApp;

public class MarkAllNotificationsReadHandlerTests
{
    private readonly INotificationRepository _notificationRepository = Substitute.For<INotificationRepository>();
    private readonly TimeProvider _timeProvider = Substitute.For<TimeProvider>();
    private readonly MarkAllNotificationsReadHandler _handler;

    public MarkAllNotificationsReadHandlerTests()
    {
        _timeProvider.GetUtcNow().Returns(DateTimeOffset.UtcNow);
        _handler = new MarkAllNotificationsReadHandler(_notificationRepository, _timeProvider);
    }

    [Fact]
    public async Task Handle_WithUnreadNotifications_MarksAllAsRead()
    {
        Guid userId = Guid.NewGuid();
        Notification n1 = Notification.Create(TenantId.New(), userId, NotificationType.TaskAssigned, "T1", "B1", _timeProvider);
        Notification n2 = Notification.Create(TenantId.New(), userId, NotificationType.Mention, "T2", "B2", _timeProvider);

        _notificationRepository
            .GetUnreadByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(new List<Notification> { n1, n2 });

        Result result = await _handler.Handle(new MarkAllNotificationsReadCommand(userId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        n1.IsRead.Should().BeTrue();
        n2.IsRead.Should().BeTrue();
        await _notificationRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithNoUnreadNotifications_StillSucceeds()
    {
        Guid userId = Guid.NewGuid();
        _notificationRepository
            .GetUnreadByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(new List<Notification>());

        Result result = await _handler.Handle(new MarkAllNotificationsReadCommand(userId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _notificationRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
