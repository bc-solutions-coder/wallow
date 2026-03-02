using Foundry.Communications.Application.Channels.InApp.Commands.MarkAllNotificationsRead;
using Foundry.Communications.Application.Channels.InApp.Commands.MarkNotificationRead;
using Foundry.Communications.Application.Channels.InApp.Commands.SendNotification;
using Foundry.Communications.Application.Channels.InApp.DTOs;
using Foundry.Communications.Application.Channels.InApp.Interfaces;
using Foundry.Communications.Domain.Channels.InApp.Entities;
using Foundry.Communications.Domain.Channels.InApp.Enums;
using Foundry.Communications.Domain.Channels.InApp.Identity;
using Foundry.Shared.Kernel.Identity;
using Foundry.Shared.Kernel.MultiTenancy;
using Foundry.Shared.Kernel.Results;

namespace Foundry.Communications.Tests.Application.Handlers;

public class NotificationHandlerTests
{
    private readonly INotificationRepository _repository;
    private readonly INotificationService _notificationService;
    private readonly ITenantContext _tenantContext;
    private readonly TenantId _tenantId;
    private readonly SendNotificationHandler _sendHandler;
    private readonly MarkNotificationReadHandler _markReadHandler;
    private readonly MarkAllNotificationsReadHandler _markAllReadHandler;

    public NotificationHandlerTests()
    {
        _repository = Substitute.For<INotificationRepository>();
        _notificationService = Substitute.For<INotificationService>();
        _tenantContext = Substitute.For<ITenantContext>();
        _tenantId = TenantId.Create(Guid.NewGuid());
        _tenantContext.TenantId.Returns(_tenantId);
        _sendHandler = new SendNotificationHandler(_repository, _notificationService, _tenantContext, TimeProvider.System);
        _markReadHandler = new MarkNotificationReadHandler(_repository, TimeProvider.System);
        _markAllReadHandler = new MarkAllNotificationsReadHandler(_repository, TimeProvider.System);
    }

    // --- SendNotification ---

    [Fact]
    public async Task Send_WithValidCommand_ReturnsSuccessWithDto()
    {
        Guid userId = Guid.NewGuid();
        SendNotificationCommand command = new(userId, NotificationType.SystemAlert, "Alert Title", "Alert body");

        Result<NotificationDto> result = await _sendHandler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Title.Should().Be("Alert Title");
        result.Value.Message.Should().Be("Alert body");
        result.Value.UserId.Should().Be(userId);
    }

    [Fact]
    public async Task Send_WithValidCommand_PersistsAndDelivers()
    {
        Guid userId = Guid.NewGuid();
        SendNotificationCommand command = new(userId, NotificationType.TaskAssigned, "Task", "Assigned to you");

        await _sendHandler.Handle(command, CancellationToken.None);

        _repository.Received(1).Add(Arg.Any<Notification>());
        await _repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _notificationService.Received(1).SendToUserAsync(
            userId, "Task", "Assigned to you",
            nameof(NotificationType.TaskAssigned),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Send_WhenDeliveryServiceThrows_PropagatesException()
    {
        Guid userId = Guid.NewGuid();
        SendNotificationCommand command = new(userId, NotificationType.SystemAlert, "Title", "Message");

        _notificationService.SendToUserAsync(
                Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("SignalR connection lost")));

        Func<Task> act = () => _sendHandler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*SignalR connection lost*");

        _repository.Received(1).Add(Arg.Any<Notification>());
        await _repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Send_WhenRepositoryThrows_PropagatesException()
    {
        SendNotificationCommand command = new(Guid.NewGuid(), NotificationType.Mention, "Title", "Body");

        _repository.When(r => r.SaveChangesAsync(Arg.Any<CancellationToken>()))
            .Do(_ => throw new InvalidOperationException("Database unavailable"));

        Func<Task> act = () => _sendHandler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Database unavailable*");

        await _notificationService.DidNotReceive().SendToUserAsync(
            Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // --- MarkNotificationRead ---

    [Fact]
    public async Task MarkRead_WhenNotificationExistsAndUserMatches_ReturnsSuccess()
    {
        Guid userId = Guid.NewGuid();
        Notification notification = Notification.Create(_tenantId, userId, NotificationType.SystemAlert, "Title", "Message", TimeProvider.System);

        _repository.GetByIdAsync(Arg.Any<NotificationId>(), Arg.Any<CancellationToken>())
            .Returns(notification);

        MarkNotificationReadCommand command = new(notification.Id.Value, userId);

        Result result = await _markReadHandler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        notification.IsRead.Should().BeTrue();
        await _repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MarkRead_WhenNotificationNotFound_ReturnsNotFoundFailure()
    {
        _repository.GetByIdAsync(Arg.Any<NotificationId>(), Arg.Any<CancellationToken>())
            .Returns((Notification?)null);

        MarkNotificationReadCommand command = new(Guid.NewGuid(), Guid.NewGuid());

        Result result = await _markReadHandler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Contain("NotFound");
        await _repository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MarkRead_WhenUserDoesNotOwnNotification_ReturnsUnauthorized()
    {
        Guid ownerId = Guid.NewGuid();
        Guid differentUserId = Guid.NewGuid();
        Notification notification = Notification.Create(_tenantId, ownerId, NotificationType.SystemAlert, "Title", "Message", TimeProvider.System);

        _repository.GetByIdAsync(Arg.Any<NotificationId>(), Arg.Any<CancellationToken>())
            .Returns(notification);

        MarkNotificationReadCommand command = new(notification.Id.Value, differentUserId);

        Result result = await _markReadHandler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Contain("Unauthorized");
        notification.IsRead.Should().BeFalse();
        await _repository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // --- MarkAllNotificationsRead (Dismiss All) ---

    [Fact]
    public async Task MarkAllRead_WithUnreadNotifications_MarksAllAsRead()
    {
        Guid userId = Guid.NewGuid();
        List<Notification> notifications = new()
        {
            Notification.Create(_tenantId, userId, NotificationType.SystemAlert, "Title 1", "Message 1", TimeProvider.System),
            Notification.Create(_tenantId, userId, NotificationType.TaskAssigned, "Title 2", "Message 2", TimeProvider.System),
            Notification.Create(_tenantId, userId, NotificationType.Mention, "Title 3", "Message 3", TimeProvider.System)
        };

        _repository.GetUnreadByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(notifications);

        MarkAllNotificationsReadCommand command = new(userId);

        Result result = await _markAllReadHandler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        notifications.Should().AllSatisfy(n => n.IsRead.Should().BeTrue());
        await _repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MarkAllRead_WithNoUnreadNotifications_ReturnsSuccess()
    {
        Guid userId = Guid.NewGuid();
        _repository.GetUnreadByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(new List<Notification>());

        MarkAllNotificationsReadCommand command = new(userId);

        Result result = await _markAllReadHandler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // --- Bulk Operations ---

    [Fact]
    public async Task Send_MultipleConcurrent_CreatesDistinctNotifications()
    {
        Guid userId = Guid.NewGuid();
        SendNotificationCommand command1 = new(userId, NotificationType.SystemAlert, "Alert 1", "Body 1");
        SendNotificationCommand command2 = new(userId, NotificationType.TaskAssigned, "Task 1", "Body 2");
        SendNotificationCommand command3 = new(userId, NotificationType.Mention, "Mention 1", "Body 3");

        Task<Result<NotificationDto>>[] tasks = new[]
        {
            _sendHandler.Handle(command1, CancellationToken.None),
            _sendHandler.Handle(command2, CancellationToken.None),
            _sendHandler.Handle(command3, CancellationToken.None)
        };
        Result<NotificationDto>[] results = await Task.WhenAll(tasks);

        results.Should().AllSatisfy(r => r.IsSuccess.Should().BeTrue());
        results.Select(r => r.Value.Id).Distinct().Should().HaveCount(3);
        _repository.Received(3).Add(Arg.Any<Notification>());
    }

    [Fact]
    public async Task MarkAllRead_WithLargeBatch_MarksAllAsRead()
    {
        Guid userId = Guid.NewGuid();
        List<Notification> notifications = Enumerable.Range(0, 50)
            .Select(_ => Notification.Create(_tenantId, userId, NotificationType.SystemAlert, "Title", "Message", TimeProvider.System))
            .ToList();

        _repository.GetUnreadByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(notifications);

        MarkAllNotificationsReadCommand command = new(userId);

        Result result = await _markAllReadHandler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        notifications.Should().AllSatisfy(n => n.IsRead.Should().BeTrue());
        await _repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // --- Full Lifecycle ---

    [Fact]
    public async Task Lifecycle_SendThenMarkRead_TransitionsCorrectly()
    {
        Guid userId = Guid.NewGuid();
        SendNotificationCommand sendCommand = new(userId, NotificationType.SystemAlert, "Alert", "Check this");

        Result<NotificationDto> sendResult = await _sendHandler.Handle(sendCommand, CancellationToken.None);

        sendResult.IsSuccess.Should().BeTrue();
        sendResult.Value.IsRead.Should().BeFalse();

        Notification notification = Notification.Create(_tenantId, userId, NotificationType.SystemAlert, "Alert", "Check this", TimeProvider.System);
        _repository.GetByIdAsync(Arg.Any<NotificationId>(), Arg.Any<CancellationToken>())
            .Returns(notification);

        MarkNotificationReadCommand markCommand = new(notification.Id.Value, userId);
        Result markResult = await _markReadHandler.Handle(markCommand, CancellationToken.None);

        markResult.IsSuccess.Should().BeTrue();
        notification.IsRead.Should().BeTrue();
    }
}
