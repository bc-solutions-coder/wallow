using Foundry.Communications.Application.Channels.InApp.Commands.MarkAllNotificationsRead;
using Foundry.Communications.Application.Channels.InApp.Interfaces;
using Foundry.Communications.Domain.Channels.InApp.Entities;
using Foundry.Communications.Domain.Enums;
using Foundry.Shared.Kernel.Identity;
using Foundry.Shared.Kernel.Results;

namespace Foundry.Communications.Tests.Application.Channels.InApp.Handlers;

public class MarkAllNotificationsReadHandlerTests
{
    private readonly INotificationRepository _repository;
    private readonly MarkAllNotificationsReadHandler _handler;

    public MarkAllNotificationsReadHandlerTests()
    {
        _repository = Substitute.For<INotificationRepository>();
        _handler = new MarkAllNotificationsReadHandler(_repository, TimeProvider.System);
    }

    [Fact]
    public async Task Handle_WithUnreadNotifications_MarksAllAsRead()
    {
        Guid userId = Guid.NewGuid();
        TenantId tenantId = TenantId.Create(Guid.NewGuid());

        List<Notification> notifications =
        [
            Notification.Create(tenantId, userId, NotificationType.SystemAlert, "Title 1", "Message 1", TimeProvider.System),
            Notification.Create(tenantId, userId, NotificationType.TaskAssigned, "Title 2", "Message 2", TimeProvider.System),
            Notification.Create(tenantId, userId, NotificationType.Mention, "Title 3", "Message 3", TimeProvider.System)
        ];

        _repository.GetUnreadByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(notifications);

        MarkAllNotificationsReadCommand command = new(userId);

        Result result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        notifications.Should().AllSatisfy(n => n.IsRead.Should().BeTrue());
        await _repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithNoUnreadNotifications_ReturnsSuccess()
    {
        Guid userId = Guid.NewGuid();
        _repository.GetUnreadByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns([]);

        MarkAllNotificationsReadCommand command = new(userId);

        Result result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PassesCancellationToken()
    {
        using CancellationTokenSource cts = new();
        Guid userId = Guid.NewGuid();

        _repository.GetUnreadByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns([]);

        MarkAllNotificationsReadCommand command = new(userId);

        await _handler.Handle(command, cts.Token);

        await _repository.Received(1).GetUnreadByUserIdAsync(userId, cts.Token);
        await _repository.Received(1).SaveChangesAsync(cts.Token);
    }
}
