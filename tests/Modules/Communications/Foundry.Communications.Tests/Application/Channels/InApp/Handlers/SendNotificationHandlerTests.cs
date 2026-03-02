using Foundry.Communications.Application.Channels.InApp.Commands.SendNotification;
using Foundry.Communications.Application.Channels.InApp.DTOs;
using Foundry.Communications.Application.Channels.InApp.Interfaces;
using Foundry.Communications.Domain.Channels.InApp.Entities;
using Foundry.Communications.Domain.Channels.InApp.Enums;
using Foundry.Shared.Kernel.Identity;
using Foundry.Shared.Kernel.MultiTenancy;
using Foundry.Shared.Kernel.Results;

namespace Foundry.Communications.Tests.Application.Channels.InApp.Handlers;

public class SendNotificationHandlerTests
{
    private readonly INotificationRepository _repository;
    private readonly INotificationService _notificationService;
    private readonly ITenantContext _tenantContext;
    private readonly SendNotificationHandler _handler;

    public SendNotificationHandlerTests()
    {
        _repository = Substitute.For<INotificationRepository>();
        _notificationService = Substitute.For<INotificationService>();
        _tenantContext = Substitute.For<ITenantContext>();
        _tenantContext.TenantId.Returns(TenantId.Create(Guid.NewGuid()));
        _handler = new SendNotificationHandler(_repository, _notificationService, _tenantContext, TimeProvider.System);
    }

    [Fact]
    public async Task Handle_WithValidCommand_ReturnsSuccess()
    {
        Guid userId = Guid.NewGuid();
        SendNotificationCommand command = new(userId, NotificationType.SystemAlert, "Test Title", "Test Message");

        Result<NotificationDto> result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Title.Should().Be("Test Title");
        result.Value.Message.Should().Be("Test Message");
        result.Value.UserId.Should().Be(userId);
    }

    [Fact]
    public async Task Handle_WithValidCommand_AddsNotificationToRepository()
    {
        SendNotificationCommand command = new(Guid.NewGuid(), NotificationType.TaskAssigned, "Title", "Message");

        await _handler.Handle(command, CancellationToken.None);

        _repository.Received(1).Add(Arg.Any<Notification>());
        await _repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithValidCommand_SendsToUserViaService()
    {
        Guid userId = Guid.NewGuid();
        SendNotificationCommand command = new(userId, NotificationType.Mention, "Title", "Message");

        await _handler.Handle(command, CancellationToken.None);

        await _notificationService.Received(1).SendToUserAsync(
            userId,
            "Title",
            "Message",
            nameof(NotificationType.Mention),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithValidCommand_ReturnsCorrectNotificationType()
    {
        SendNotificationCommand command = new(Guid.NewGuid(), NotificationType.BillingInvoice, "Invoice", "You have an invoice");

        Result<NotificationDto> result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Type.Should().Be(nameof(NotificationType.BillingInvoice));
    }

    [Fact]
    public async Task Handle_PassesCancellationToken()
    {
        using CancellationTokenSource cts = new();
        SendNotificationCommand command = new(Guid.NewGuid(), NotificationType.SystemAlert, "Title", "Message");

        await _handler.Handle(command, cts.Token);

        await _repository.Received(1).SaveChangesAsync(cts.Token);
        await _notificationService.Received(1).SendToUserAsync(
            Arg.Any<Guid>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            cts.Token);
    }
}
