using Foundry.Communications.Application.Channels.InApp.Commands.MarkNotificationRead;
using Foundry.Communications.Application.Channels.InApp.Interfaces;
using Foundry.Communications.Domain.Channels.InApp.Entities;
using Foundry.Communications.Domain.Channels.InApp.Enums;
using Foundry.Communications.Domain.Channels.InApp.Identity;
using Foundry.Shared.Kernel.Identity;
using Foundry.Shared.Kernel.Results;

namespace Foundry.Communications.Tests.Application.Channels.InApp.Handlers;

public class MarkNotificationReadHandlerTests
{
    private readonly INotificationRepository _repository;
    private readonly MarkNotificationReadHandler _handler;

    public MarkNotificationReadHandlerTests()
    {
        _repository = Substitute.For<INotificationRepository>();
        _handler = new MarkNotificationReadHandler(_repository, TimeProvider.System);
    }

    [Fact]
    public async Task Handle_WhenNotificationExistsAndUserMatches_ReturnsSuccess()
    {
        Guid userId = Guid.NewGuid();
        Notification notification = Notification.Create(
            TenantId.Create(Guid.NewGuid()),
            userId,
            NotificationType.SystemAlert,
            "Title",
            "Message", TimeProvider.System);

        _repository.GetByIdAsync(Arg.Any<NotificationId>(), Arg.Any<CancellationToken>())
            .Returns(notification);

        MarkNotificationReadCommand command = new(notification.Id.Value, userId);

        Result result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenNotificationNotFound_ReturnsNotFoundFailure()
    {
        _repository.GetByIdAsync(Arg.Any<NotificationId>(), Arg.Any<CancellationToken>())
            .Returns((Notification?)null);

        MarkNotificationReadCommand command = new(Guid.NewGuid(), Guid.NewGuid());

        Result result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Contain("NotFound");
        await _repository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenUserDoesNotOwnNotification_ReturnsUnauthorized()
    {
        Guid ownerId = Guid.NewGuid();
        Guid differentUserId = Guid.NewGuid();
        Notification notification = Notification.Create(
            TenantId.Create(Guid.NewGuid()),
            ownerId,
            NotificationType.SystemAlert,
            "Title",
            "Message", TimeProvider.System);

        _repository.GetByIdAsync(Arg.Any<NotificationId>(), Arg.Any<CancellationToken>())
            .Returns(notification);

        MarkNotificationReadCommand command = new(notification.Id.Value, differentUserId);

        Result result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Contain("Unauthorized");
        await _repository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PassesCancellationToken()
    {
        using CancellationTokenSource cts = new();
        Guid userId = Guid.NewGuid();
        Notification notification = Notification.Create(
            TenantId.Create(Guid.NewGuid()),
            userId,
            NotificationType.SystemAlert,
            "Title",
            "Message", TimeProvider.System);

        _repository.GetByIdAsync(Arg.Any<NotificationId>(), Arg.Any<CancellationToken>())
            .Returns(notification);

        MarkNotificationReadCommand command = new(notification.Id.Value, userId);

        await _handler.Handle(command, cts.Token);

        await _repository.Received(1).GetByIdAsync(Arg.Any<NotificationId>(), cts.Token);
        await _repository.Received(1).SaveChangesAsync(cts.Token);
    }
}
