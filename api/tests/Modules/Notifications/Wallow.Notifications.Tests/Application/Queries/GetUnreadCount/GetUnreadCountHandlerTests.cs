using Wallow.Notifications.Application.Channels.InApp.Interfaces;
using Wallow.Notifications.Application.Channels.InApp.Queries.GetUnreadCount;
using Wallow.Shared.Kernel.Results;

namespace Wallow.Notifications.Tests.Application.Queries.GetUnreadCount;

public class GetUnreadCountHandlerTests
{
    private readonly INotificationRepository _notificationRepository = Substitute.For<INotificationRepository>();
    private readonly GetUnreadCountHandler _handler;

    public GetUnreadCountHandlerTests()
    {
        _handler = new GetUnreadCountHandler(_notificationRepository);
    }

    [Fact]
    public async Task Handle_ReturnsUnreadCount()
    {
        Guid userId = Guid.NewGuid();
        GetUnreadCountQuery query = new(userId);

        _notificationRepository
            .GetUnreadCountAsync(userId, Arg.Any<CancellationToken>())
            .Returns(5);

        Result<int> result = await _handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(5);
    }

    [Fact]
    public async Task Handle_NoUnreadNotifications_ReturnsZero()
    {
        Guid userId = Guid.NewGuid();
        GetUnreadCountQuery query = new(userId);

        _notificationRepository
            .GetUnreadCountAsync(userId, Arg.Any<CancellationToken>())
            .Returns(0);

        Result<int> result = await _handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(0);
    }
}
