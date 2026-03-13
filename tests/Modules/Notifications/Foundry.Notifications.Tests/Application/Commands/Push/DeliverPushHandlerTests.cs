using Foundry.Notifications.Application.Channels.Push.Commands.DeliverPush;
using Foundry.Notifications.Application.Channels.Push.Interfaces;
using Foundry.Notifications.Domain.Channels.Push.Entities;
using Foundry.Notifications.Domain.Channels.Push.Enums;
using Foundry.Notifications.Domain.Channels.Push.Identity;
using Foundry.Shared.Kernel.Identity;
using Microsoft.Extensions.Logging.Abstractions;

namespace Foundry.Notifications.Tests.Application.Commands.Push;

public class DeliverPushHandlerTests
{
    private readonly IPushProviderFactory _pushProviderFactory = Substitute.For<IPushProviderFactory>();
    private readonly IPushMessageRepository _pushMessageRepository = Substitute.For<IPushMessageRepository>();
    private readonly TimeProvider _timeProvider = Substitute.For<TimeProvider>();
    private readonly DeliverPushHandler _handler;

    public DeliverPushHandlerTests()
    {
        _timeProvider.GetUtcNow().Returns(DateTimeOffset.UtcNow);
        _handler = new DeliverPushHandler(
            _pushProviderFactory,
            _pushMessageRepository,
            _timeProvider,
            NullLogger<DeliverPushHandler>.Instance);
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
            "device-token",
            PushPlatform.Fcm);

        await _handler.Handle(command, CancellationToken.None);

        _pushProviderFactory.DidNotReceive().GetProvider(Arg.Any<PushPlatform>());
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

        _pushProviderFactory.GetProvider(PushPlatform.Fcm).Returns(provider);

        DeliverPushCommand command = new(pushMessage.Id, DeviceRegistrationId.New(), "device-token", PushPlatform.Fcm);

        await _handler.Handle(command, CancellationToken.None);

        pushMessage.Status.Should().Be(PushStatus.Delivered);
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

        _pushProviderFactory.GetProvider(PushPlatform.Apns).Returns(provider);

        DeliverPushCommand command = new(pushMessage.Id, DeviceRegistrationId.New(), "device-token", PushPlatform.Apns);

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

        _pushProviderFactory.GetProvider(PushPlatform.Fcm).Returns(provider);

        DeliverPushCommand command = new(pushMessage.Id, DeviceRegistrationId.New(), "device-token", PushPlatform.Fcm);

        Func<Task> act = () => _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        pushMessage.Status.Should().Be(PushStatus.Failed);
        await _pushMessageRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
