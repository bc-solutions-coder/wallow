using Wallow.Notifications.Application.Channels.InApp.Commands.SendNotification;
using Wallow.Notifications.Application.Channels.InApp.DTOs;
using Wallow.Notifications.Application.Channels.InApp.Interfaces;
using Wallow.Notifications.Domain.Channels.InApp.Entities;
using Wallow.Notifications.Domain.Enums;
using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.MultiTenancy;
using Wallow.Shared.Kernel.Results;

namespace Wallow.Notifications.Tests.Application.Commands.InApp;

public class SendNotificationHandlerTests
{
    private readonly INotificationRepository _notificationRepository = Substitute.For<INotificationRepository>();
    private readonly INotificationService _notificationService = Substitute.For<INotificationService>();
    private readonly ITenantContext _tenantContext = Substitute.For<ITenantContext>();
    private readonly TimeProvider _timeProvider = Substitute.For<TimeProvider>();
    private readonly SendNotificationHandler _handler;

    public SendNotificationHandlerTests()
    {
        _timeProvider.GetUtcNow().Returns(DateTimeOffset.UtcNow);
        _tenantContext.TenantId.Returns(TenantId.New());
        _handler = new SendNotificationHandler(
            _notificationRepository,
            _notificationService,
            _tenantContext,
            _timeProvider);
    }

    [Fact]
    public async Task Handle_WithValidCommand_CreatesAndSavesNotification()
    {
        SendNotificationCommand command = new(
            UserId: Guid.NewGuid(),
            Type: NotificationType.TaskAssigned,
            Title: "Task Assigned",
            Message: "You have a new task");

        Result<NotificationDto> result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _notificationRepository.Received(1).Add(Arg.Any<Notification>());
        await _notificationRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithValidCommand_SendsSignalRNotification()
    {
        Guid userId = Guid.NewGuid();
        SendNotificationCommand command = new(
            UserId: userId,
            Type: NotificationType.Mention,
            Title: "You were mentioned",
            Message: "Someone mentioned you");

        await _handler.Handle(command, CancellationToken.None);

        await _notificationService.Received(1).SendToUserAsync(
            userId,
            "You were mentioned",
            "Someone mentioned you",
            NotificationType.Mention.ToString(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ReturnsNotificationDtoWithCorrectData()
    {
        Guid userId = Guid.NewGuid();
        SendNotificationCommand command = new(
            UserId: userId,
            Type: NotificationType.SystemAlert,
            Title: "System Alert",
            Message: "System maintenance tonight");

        Result<NotificationDto> result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.UserId.Should().Be(userId);
        result.Value.Title.Should().Be("System Alert");
        result.Value.Message.Should().Be("System maintenance tonight");
        result.Value.IsRead.Should().BeFalse();
    }
}
