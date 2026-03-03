using Foundry.Communications.Application.Channels.InApp.Commands.ArchiveNotification;
using Foundry.Communications.Application.Channels.InApp.Interfaces;
using Foundry.Communications.Domain.Channels.InApp.Entities;
using Foundry.Communications.Domain.Enums;
using Foundry.Communications.Domain.Channels.InApp.Identity;
using Foundry.Shared.Kernel.Identity;
using Foundry.Shared.Kernel.Results;

namespace Foundry.Communications.Tests.Application.Channels.InApp.Handlers;

public class ArchiveNotificationHandlerTests
{
    private readonly INotificationRepository _repository;
    private readonly ArchiveNotificationHandler _handler;

    public ArchiveNotificationHandlerTests()
    {
        _repository = Substitute.For<INotificationRepository>();
        _handler = new ArchiveNotificationHandler(_repository, TimeProvider.System);
    }

    [Fact]
    public async Task Handle_WhenNotificationExists_ArchivesAndReturnsSuccess()
    {
        TenantId tenantId = TenantId.Create(Guid.NewGuid());
        Guid userId = Guid.NewGuid();
        Notification notification = Notification.Create(tenantId, userId, NotificationType.SystemAlert, "Title", "Message", TimeProvider.System);

        _repository.GetByIdAsync(Arg.Any<NotificationId>(), Arg.Any<CancellationToken>())
            .Returns(notification);

        ArchiveNotificationCommand command = new(notification.Id, tenantId, userId);

        Result result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        notification.IsArchived.Should().BeTrue();
        await _repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenNotificationNotFound_ReturnsNotFoundFailure()
    {
        _repository.GetByIdAsync(Arg.Any<NotificationId>(), Arg.Any<CancellationToken>())
            .Returns((Notification?)null);

        ArchiveNotificationCommand command = new(
            NotificationId.New(),
            TenantId.Create(Guid.NewGuid()),
            Guid.NewGuid());

        Result result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Contain("NotFound");
        await _repository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenTenantDoesNotMatch_ReturnsUnauthorizedFailure()
    {
        TenantId ownerTenant = TenantId.Create(Guid.NewGuid());
        TenantId differentTenant = TenantId.Create(Guid.NewGuid());
        Notification notification = Notification.Create(ownerTenant, Guid.NewGuid(), NotificationType.SystemAlert, "Title", "Message", TimeProvider.System);

        _repository.GetByIdAsync(Arg.Any<NotificationId>(), Arg.Any<CancellationToken>())
            .Returns(notification);

        ArchiveNotificationCommand command = new(notification.Id, differentTenant, Guid.NewGuid());

        Result result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Contain("Unauthorized");
        await _repository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PassesCancellationToken()
    {
        using CancellationTokenSource cts = new();
        TenantId tenantId = TenantId.Create(Guid.NewGuid());
        Guid userId = Guid.NewGuid();
        Notification notification = Notification.Create(tenantId, userId, NotificationType.SystemAlert, "Title", "Message", TimeProvider.System);

        _repository.GetByIdAsync(Arg.Any<NotificationId>(), Arg.Any<CancellationToken>())
            .Returns(notification);

        ArchiveNotificationCommand command = new(notification.Id, tenantId, userId);

        await _handler.Handle(command, cts.Token);

        await _repository.Received(1).GetByIdAsync(Arg.Any<NotificationId>(), cts.Token);
        await _repository.Received(1).SaveChangesAsync(cts.Token);
    }
}
