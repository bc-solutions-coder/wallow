using Microsoft.Extensions.Logging;
using Wallow.Notifications.Application.Channels.Push.Commands.DeliverPush;
using Wallow.Notifications.Application.Channels.Push.Interfaces;
using Wallow.Notifications.Domain.Channels.Push;
using Wallow.Notifications.Domain.Channels.Push.Entities;
using Wallow.Notifications.Domain.Channels.Push.Enums;
using Wallow.Notifications.Domain.Channels.Push.Identity;
using Wallow.Shared.Kernel.Identity;

namespace Wallow.Notifications.Tests.Application.Commands.Push;

public class DeliverPushHandlerTests
{
    private readonly IPushProviderFactory _pushProviderFactory = Substitute.For<IPushProviderFactory>();
    private readonly IPushMessageRepository _pushMessageRepository = Substitute.For<IPushMessageRepository>();
    private readonly IDeviceRegistrationRepository _devices = Substitute.For<IDeviceRegistrationRepository>();
    private readonly TimeProvider _timeProvider = Substitute.For<TimeProvider>();
    private readonly Wolverine.IMessageBus _bus = Substitute.For<Wolverine.IMessageBus>();
    private readonly DeliverPushHandler _handler;

#pragma warning disable CA2000 // LoggerFactory disposal not needed in tests
    public DeliverPushHandlerTests()
    {
        _timeProvider.GetUtcNow().Returns(DateTimeOffset.UtcNow);
        _handler = new DeliverPushHandler(
            _pushProviderFactory,
            _pushMessageRepository,
            _devices,
            _timeProvider,
            LoggerFactory.Create(b => b.AddSimpleConsole().SetMinimumLevel(LogLevel.Trace))
                .CreateLogger<DeliverPushHandler>(), _bus);
    }
#pragma warning restore CA2000

    [Theory]
    [InlineData(true, false, 0, true, false)]
    [InlineData(false, true, 0, false, true)]
    [InlineData(false, true, 2, false, false)]
    public async Task Failure_DeactivatesOnlyExpiredSubscriptionsAndBoundsRetries(bool expired, bool retryable, int attempt, bool deactivated, bool scheduled)
    {
        PushMessage message = PushMessage.Create(TenantId.New(), UserId.New(), "Title", "Body", _timeProvider);
        _pushMessageRepository.GetByIdAsync(message.Id, Arg.Any<CancellationToken>()).Returns(message);
        DeviceRegistration device = RegisterDevice(message, PushPlatform.WebPush);
        IPushProvider provider = Substitute.For<IPushProvider>();
        provider.SendAsync(message, device.Token, Arg.Any<CancellationToken>())
            .Returns(new PushDeliveryResult(false, "Failure", expired, retryable, TimeSpan.FromSeconds(120)));
        _pushProviderFactory.GetProviderAsync(device).Returns(provider);

        await _handler.Handle(new DeliverPushCommand(message.Id, device.Id, message.TenantId.Value, attempt), CancellationToken.None);

        device.IsActive.Should().Be(!deactivated);
        await _devices.Received(deactivated ? 1 : 0).SaveDeactivationAsync(device, Arg.Any<CancellationToken>());
        await _bus.Received(scheduled ? 1 : 0).PublishAsync(
            Arg.Is<DeliverPushCommand>(next => next.DeviceRegistrationId == device.Id && next.Attempt == attempt + 1),
            Arg.Is<Wolverine.DeliveryOptions>(options => options.ScheduleDelay == TimeSpan.FromSeconds(120)));
    }

    [Fact]
    public async Task Handle_WhenPushMessageNotFound_LogsAndReturns()
    {
        _pushMessageRepository
            .GetByIdAsync(Arg.Any<PushMessageId>(), Arg.Any<CancellationToken>())
            .Returns((PushMessage?)null);

        DeliverPushCommand command = new(
            PushMessageId.New(),
            DeviceRegistrationId.New(),
            Guid.NewGuid());

        await _handler.Handle(command, CancellationToken.None);

        await _pushProviderFactory.DidNotReceive().GetProviderAsync(Arg.Any<Wallow.Notifications.Domain.Channels.Push.DeviceRegistration>());
    }

    [Fact]
    public async Task Handle_WhenProviderSucceeds_MarksDelivered()
    {
        TenantId tenantId = TenantId.New();
        PushMessage pushMessage = PushMessage.Create(
            tenantId, new UserId(Guid.NewGuid()), "Title", "Body", _timeProvider);

        _pushMessageRepository
            .GetByIdAsync(pushMessage.Id, Arg.Any<CancellationToken>())
            .Returns(pushMessage);

        IPushProvider provider = Substitute.For<IPushProvider>();
        provider.SendAsync(Arg.Any<PushMessage>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new PushDeliveryResult(true, null));

        _pushProviderFactory.GetProviderAsync(Arg.Any<Wallow.Notifications.Domain.Channels.Push.DeviceRegistration>()).Returns(provider);

        DeviceRegistration device = RegisterDevice(pushMessage, PushPlatform.Fcm);
        DeliverPushCommand command = new(pushMessage.Id, device.Id, tenantId.Value);

        await _handler.Handle(command, CancellationToken.None);

        pushMessage.Status.Should().Be(PushStatus.Accepted);
        _pushMessageRepository.Received(1).Update(pushMessage);
        await _pushMessageRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenProviderFails_MarksMessageFailed()
    {
        TenantId tenantId = TenantId.New();
        PushMessage pushMessage = PushMessage.Create(
            tenantId, new UserId(Guid.NewGuid()), "Title", "Body", _timeProvider);

        _pushMessageRepository
            .GetByIdAsync(pushMessage.Id, Arg.Any<CancellationToken>())
            .Returns(pushMessage);

        IPushProvider provider = Substitute.For<IPushProvider>();
        provider.SendAsync(Arg.Any<PushMessage>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new PushDeliveryResult(false, "Token expired"));

        _pushProviderFactory.GetProviderAsync(Arg.Any<Wallow.Notifications.Domain.Channels.Push.DeviceRegistration>()).Returns(provider);

        DeviceRegistration device = RegisterDevice(pushMessage, PushPlatform.Apns);
        DeliverPushCommand command = new(pushMessage.Id, device.Id, tenantId.Value);

        await _handler.Handle(command, CancellationToken.None);

        pushMessage.Status.Should().Be(PushStatus.Failed);
        pushMessage.FailureReason.Should().Be("Token expired");
    }

    [Fact]
    public async Task Handle_WhenProviderThrows_MarksFailedAndRethrows()
    {
        TenantId tenantId = TenantId.New();
        PushMessage pushMessage = PushMessage.Create(
            tenantId, new UserId(Guid.NewGuid()), "Title", "Body", _timeProvider);

        _pushMessageRepository
            .GetByIdAsync(pushMessage.Id, Arg.Any<CancellationToken>())
            .Returns(pushMessage);

        IPushProvider provider = Substitute.For<IPushProvider>();
        provider.SendAsync(Arg.Any<PushMessage>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<Task<PushDeliveryResult>>(Task.FromException<PushDeliveryResult>(new InvalidOperationException("Network failure")));

        _pushProviderFactory.GetProviderAsync(Arg.Any<Wallow.Notifications.Domain.Channels.Push.DeviceRegistration>()).Returns(provider);

        DeviceRegistration device = RegisterDevice(pushMessage, PushPlatform.Fcm);
        DeliverPushCommand command = new(pushMessage.Id, device.Id, tenantId.Value);

        Func<Task> act = () => _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        pushMessage.Status.Should().Be(PushStatus.Failed);
        await _pushMessageRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    private DeviceRegistration RegisterDevice(PushMessage message, PushPlatform platform)
    {
        DeviceRegistration device = DeviceRegistration.Register(message.RecipientId, message.TenantId, platform, "device-token", DateTimeOffset.UtcNow);
        _devices.GetByIdAsync(device.Id, Arg.Any<CancellationToken>()).Returns(device);
        return device;
    }

}
