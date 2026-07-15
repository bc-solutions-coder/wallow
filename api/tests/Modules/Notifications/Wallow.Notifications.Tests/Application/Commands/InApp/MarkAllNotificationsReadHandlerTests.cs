using Wallow.Notifications.Application.Channels.InApp.Commands.MarkAllNotificationsRead;
using Wallow.Notifications.Application.Channels.InApp.Interfaces;
using Wallow.Shared.Kernel.Results;

namespace Wallow.Notifications.Tests.Application.Commands.InApp;

public class MarkAllNotificationsReadHandlerTests
{
    private readonly INotificationRepository _notificationRepository = Substitute.For<INotificationRepository>();
    private readonly TimeProvider _timeProvider = Substitute.For<TimeProvider>();
    private readonly MarkAllNotificationsReadHandler _handler;
    private readonly DateTimeOffset _now = new(2024, 6, 15, 12, 0, 0, TimeSpan.Zero);

    public MarkAllNotificationsReadHandlerTests()
    {
        _timeProvider.GetUtcNow().Returns(_now);
        _handler = new MarkAllNotificationsReadHandler(_notificationRepository, _timeProvider);
    }

    [Fact]
    public async Task Handle_CallsMarkAllAsReadWithCorrectParameters()
    {
        Guid userId = Guid.NewGuid();

        Result result = await _handler.Handle(new MarkAllNotificationsReadCommand(userId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _notificationRepository.Received(1).MarkAllAsReadAsync(
            userId,
            _now.UtcDateTime,
            Arg.Any<CancellationToken>());
    }
}
