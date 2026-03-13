using Foundry.Notifications.Application.Channels.InApp.Commands.MarkNotificationRead;
using Foundry.Notifications.Application.Channels.InApp.Interfaces;
using Foundry.Notifications.Domain.Channels.InApp.Entities;
using Foundry.Notifications.Domain.Channels.InApp.Identity;
using Foundry.Notifications.Domain.Enums;
using Foundry.Shared.Kernel.Identity;
using Foundry.Shared.Kernel.Results;

namespace Foundry.Notifications.Tests.Application.Commands.InApp;

public class MarkNotificationReadHandlerTests
{
    private readonly INotificationRepository _notificationRepository = Substitute.For<INotificationRepository>();
    private readonly TimeProvider _timeProvider = Substitute.For<TimeProvider>();
    private readonly MarkNotificationReadHandler _handler;

    public MarkNotificationReadHandlerTests()
    {
        _timeProvider.GetUtcNow().Returns(DateTimeOffset.UtcNow);
        _handler = new MarkNotificationReadHandler(_notificationRepository, _timeProvider);
    }

    [Fact]
    public async Task Handle_WhenNotificationFound_MarksAsReadAndSaves()
    {
        Guid userId = Guid.NewGuid();
        Notification notification = Notification.Create(
            TenantId.New(), userId, NotificationType.TaskAssigned, "Title", "Body", _timeProvider);

        _notificationRepository
            .GetByIdAsync(Arg.Any<NotificationId>(), Arg.Any<CancellationToken>())
            .Returns(notification);

        MarkNotificationReadCommand command = new(notification.Id.Value, userId);

        Result result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        notification.IsRead.Should().BeTrue();
        await _notificationRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenNotificationNotFound_ReturnsNotFoundFailure()
    {
        _notificationRepository
            .GetByIdAsync(Arg.Any<NotificationId>(), Arg.Any<CancellationToken>())
            .Returns((Notification?)null);

        MarkNotificationReadCommand command = new(Guid.NewGuid(), Guid.NewGuid());

        Result result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("Notification.NotFound");
    }

    [Fact]
    public async Task Handle_WhenDifferentUser_ReturnsUnauthorizedFailure()
    {
        Guid ownerId = Guid.NewGuid();
        Guid differentUserId = Guid.NewGuid();

        Notification notification = Notification.Create(
            TenantId.New(), ownerId, NotificationType.Mention, "Title", "Body", _timeProvider);

        _notificationRepository
            .GetByIdAsync(Arg.Any<NotificationId>(), Arg.Any<CancellationToken>())
            .Returns(notification);

        MarkNotificationReadCommand command = new(notification.Id.Value, differentUserId);

        Result result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
    }
}
