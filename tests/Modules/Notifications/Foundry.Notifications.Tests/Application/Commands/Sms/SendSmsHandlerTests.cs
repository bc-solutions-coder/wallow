using Foundry.Notifications.Application.Channels.Sms.Commands.SendSms;
using Foundry.Notifications.Application.Channels.Sms.Interfaces;
using Foundry.Notifications.Application.Preferences.Interfaces;
using Foundry.Notifications.Domain.Channels.Sms.Entities;
using Foundry.Notifications.Domain.Preferences;
using Foundry.Shared.Kernel.Identity;
using Foundry.Shared.Kernel.MultiTenancy;
using Foundry.Shared.Kernel.Results;

namespace Foundry.Notifications.Tests.Application.Commands.Sms;

public class SendSmsHandlerTests
{
    private readonly ISmsMessageRepository _smsMessageRepository = Substitute.For<ISmsMessageRepository>();
    private readonly ISmsProvider _smsProvider = Substitute.For<ISmsProvider>();
    private readonly ITenantContext _tenantContext = Substitute.For<ITenantContext>();
    private readonly TimeProvider _timeProvider = Substitute.For<TimeProvider>();
    private readonly INotificationPreferenceChecker _preferenceChecker = Substitute.For<INotificationPreferenceChecker>();
    private readonly SendSmsHandler _handler;

    public SendSmsHandlerTests()
    {
        _timeProvider.GetUtcNow().Returns(DateTimeOffset.UtcNow);
        _tenantContext.TenantId.Returns(TenantId.New());
        _handler = new SendSmsHandler(
            _smsMessageRepository,
            _smsProvider,
            _tenantContext,
            _timeProvider,
            _preferenceChecker);
    }

    [Fact]
    public async Task Handle_WithValidCommand_SendsSmsAndSaves()
    {
        SendSmsCommand command = new("+12025550100", "Hello!", null);

        _smsProvider.SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new SmsDeliveryResult(true, "SM123", null));

        Result result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _smsMessageRepository.Received(1).Add(Arg.Any<SmsMessage>());
        await _smsProvider.Received(1).SendAsync("+12025550100", "Hello!", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenPreferenceDisabled_SkipsSmsAndReturnsSuccess()
    {
        UserId userId = new(Guid.NewGuid());
        SendSmsCommand command = new("+12025550100", "Hello!", null, userId, "Alert");

        _preferenceChecker
            .IsChannelEnabledAsync(userId, ChannelType.Sms, "Alert", Arg.Any<CancellationToken>())
            .Returns(false);

        Result result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _smsMessageRepository.DidNotReceive().Add(Arg.Any<SmsMessage>());
        await _smsProvider.DidNotReceive().SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenProviderReturnsFailed_MarksSmsAsFailed()
    {
        SendSmsCommand command = new("+12025550100", "Hello!", null);

        _smsProvider.SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new SmsDeliveryResult(false, null, "Invalid number"));

        Result result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _smsMessageRepository.Received(2).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenProviderThrows_MarksSmsAsFailed()
    {
        SendSmsCommand command = new("+12025550100", "Hello!", null);

        _smsProvider.SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<Task<SmsDeliveryResult>>(Task.FromException<SmsDeliveryResult>(new InvalidOperationException("Network error")));

        Result result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _smsMessageRepository.Received(2).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenNoUserIdOrNotificationType_SkipsPreferenceCheck()
    {
        SendSmsCommand command = new("+12025550100", "Hello!");

        _smsProvider.SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new SmsDeliveryResult(true, "SM123", null));

        await _handler.Handle(command, CancellationToken.None);

        await _preferenceChecker.DidNotReceive().IsChannelEnabledAsync(
            Arg.Any<UserId>(), Arg.Any<ChannelType>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
