using Wallow.Notifications.Application.Channels.Push.Commands.SendPush;
using Wallow.Notifications.Application.Channels.Push.Interfaces;
using Wallow.Notifications.Application.Preferences.Interfaces;
using Wallow.Notifications.Domain.Channels.Push;
using Wallow.Notifications.Domain.Channels.Push.Entities;
using Wallow.Notifications.Domain.Channels.Push.Enums;
using Wallow.Notifications.Domain.Preferences;
using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.Results;
using Wolverine;

namespace Wallow.Notifications.Tests.Application.Commands.Push;

public class SendPushHandlerTests
{
    private readonly INotificationPreferenceChecker _preferenceChecker = Substitute.For<INotificationPreferenceChecker>();
    private readonly IPushMessageRepository _pushMessageRepository = Substitute.For<IPushMessageRepository>();
    private readonly IDeviceRegistrationRepository _deviceRegistrationRepository = Substitute.For<IDeviceRegistrationRepository>();
    private readonly IMessageBus _messageBus = Substitute.For<IMessageBus>();
    private readonly TimeProvider _timeProvider = Substitute.For<TimeProvider>();
    private readonly SendPushHandler _handler;

    public SendPushHandlerTests()
    {
        _timeProvider.GetUtcNow().Returns(DateTimeOffset.UtcNow);
        _handler = new SendPushHandler(
            _preferenceChecker,
            _pushMessageRepository,
            _deviceRegistrationRepository,
            _messageBus,
            _timeProvider);
    }

    [Fact]
    public async Task Handle_WhenPreferenceEnabled_CreatesMessageAndPublishesToDevices()
    {
        UserId recipientId = new(Guid.NewGuid());
        TenantId tenantId = TenantId.New();

        _preferenceChecker
            .IsChannelEnabledAsync(recipientId, ChannelType.Push, "Alert", Arg.Any<CancellationToken>())
            .Returns(true);

        DeviceRegistration device = DeviceRegistration.Register(
            recipientId, tenantId, PushPlatform.Fcm, "device-token", DateTimeOffset.UtcNow);

        _deviceRegistrationRepository
            .GetActiveByUserAsync(recipientId, Arg.Any<CancellationToken>())
            .Returns(new List<DeviceRegistration> { device });

        SendPushCommand command = new(recipientId, tenantId, "Alert Title", "Alert body", "Alert");

        Result result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _pushMessageRepository.Received(1).Add(Arg.Any<PushMessage>());
        await _pushMessageRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _messageBus.Received(1).PublishAsync(Arg.Any<object>());
    }

    [Fact]
    public async Task Handle_WhenPreferenceDisabled_SkipsPush()
    {
        UserId recipientId = new(Guid.NewGuid());
        TenantId tenantId = TenantId.New();

        _preferenceChecker
            .IsChannelEnabledAsync(recipientId, ChannelType.Push, "Alert", Arg.Any<CancellationToken>())
            .Returns(false);

        SendPushCommand command = new(recipientId, tenantId, "Title", "Body", "Alert");

        Result result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _pushMessageRepository.DidNotReceive().Add(Arg.Any<PushMessage>());
        await _messageBus.DidNotReceive().PublishAsync(Arg.Any<object>());
    }

    [Fact]
    public async Task Handle_WhenUserHasNoDevices_CreatesMessageButPublishesNothing()
    {
        UserId recipientId = new(Guid.NewGuid());

        _preferenceChecker
            .IsChannelEnabledAsync(recipientId, ChannelType.Push, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(true);

        _deviceRegistrationRepository
            .GetActiveByUserAsync(recipientId, Arg.Any<CancellationToken>())
            .Returns(new List<DeviceRegistration>());

        SendPushCommand command = new(recipientId, TenantId.New(), "Title", "Body", "Type");

        Result result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _pushMessageRepository.Received(1).Add(Arg.Any<PushMessage>());
        await _messageBus.DidNotReceive().PublishAsync(Arg.Any<object>());
    }
}
