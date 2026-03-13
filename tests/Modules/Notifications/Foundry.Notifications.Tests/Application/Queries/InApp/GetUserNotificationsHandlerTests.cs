using Foundry.Notifications.Application.Channels.InApp.DTOs;
using Foundry.Notifications.Application.Channels.InApp.Interfaces;
using Foundry.Notifications.Application.Channels.InApp.Queries.GetUserNotifications;
using Foundry.Notifications.Domain.Channels.InApp.Entities;
using Foundry.Notifications.Domain.Enums;
using Foundry.Shared.Kernel.Identity;
using Foundry.Shared.Kernel.Pagination;
using Foundry.Shared.Kernel.Results;

namespace Foundry.Notifications.Tests.Application.Queries.InApp;

public class GetUserNotificationsHandlerTests
{
    private readonly INotificationRepository _notificationRepository = Substitute.For<INotificationRepository>();
    private readonly GetUserNotificationsHandler _handler;

    public GetUserNotificationsHandlerTests()
    {
        _handler = new GetUserNotificationsHandler(_notificationRepository);
    }

    [Fact]
    public async Task Handle_ReturnsPagedNotificationDtos()
    {
        Guid userId = Guid.NewGuid();
        Notification notification = Notification.Create(
            TenantId.New(), userId, NotificationType.TaskAssigned, "Title", "Body", TimeProvider.System);

        PagedResult<Notification> pagedResult = new(
            new List<Notification> { notification }, 1, 1, 20);

        _notificationRepository
            .GetByUserIdPagedAsync(userId, 1, 20, Arg.Any<CancellationToken>())
            .Returns(pagedResult);

        Result<PagedResult<NotificationDto>> result = await _handler.Handle(
            new GetUserNotificationsQuery(userId, 1, 20), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(1);
        result.Value.Items[0].UserId.Should().Be(userId);
        result.Value.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WhenNoNotifications_ReturnsEmptyPagedResult()
    {
        Guid userId = Guid.NewGuid();
        PagedResult<Notification> pagedResult = new(new List<Notification>(), 0, 1, 20);

        _notificationRepository
            .GetByUserIdPagedAsync(userId, 1, 20, Arg.Any<CancellationToken>())
            .Returns(pagedResult);

        Result<PagedResult<NotificationDto>> result = await _handler.Handle(
            new GetUserNotificationsQuery(userId, 1, 20), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().BeEmpty();
        result.Value.TotalCount.Should().Be(0);
    }
}
