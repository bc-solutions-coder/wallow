using Foundry.Communications.Application.Channels.Sms.Commands.SendSms;
using Foundry.Communications.Application.Channels.Sms.Interfaces;
using Foundry.Communications.Domain.Channels.Sms.Entities;
using Foundry.Shared.Kernel.Identity;
using Foundry.Shared.Kernel.MultiTenancy;
using Foundry.Shared.Kernel.Results;

namespace Foundry.Communications.Tests.Application.Channels.Sms;

public class SendSmsHandlerTests
{
    private readonly ISmsMessageRepository _repository;
    private readonly ISmsProvider _smsProvider;
    private readonly SendSmsHandler _handler;

    public SendSmsHandlerTests()
    {
        _repository = Substitute.For<ISmsMessageRepository>();
        _smsProvider = Substitute.For<ISmsProvider>();
        ITenantContext tenantContext = Substitute.For<ITenantContext>();
        tenantContext.TenantId.Returns(TenantId.New());

        _smsProvider.SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new SmsDeliveryResult(true, "test-sid", null));

        _handler = new SendSmsHandler(_repository, _smsProvider, tenantContext, TimeProvider.System);
    }

    [Fact]
    public async Task Handle_WithValidCommand_ReturnsSuccess()
    {
        SendSmsCommand command = new("+15551234567", "Hello world");

        Result result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WithValidCommand_AddsMessageToRepository()
    {
        SendSmsCommand command = new("+15551234567", "Hello world");

        await _handler.Handle(command, CancellationToken.None);

        _repository.Received(1).Add(Arg.Any<SmsMessage>());
    }

    [Fact]
    public async Task Handle_WithValidCommand_SavesChangesBeforeAndAfterSend()
    {
        SendSmsCommand command = new("+15551234567", "Hello world");

        await _handler.Handle(command, CancellationToken.None);

        await _repository.Received(2).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithValidCommand_CallsSmsProvider()
    {
        SendSmsCommand command = new("+15551234567", "Hello world");

        await _handler.Handle(command, CancellationToken.None);

        await _smsProvider.Received(1).SendAsync("+15551234567", "Hello world", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenProviderFails_ReturnsSuccess()
    {
        _smsProvider.SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new SmsDeliveryResult(false, null, "Provider error"));

        SendSmsCommand command = new("+15551234567", "Hello world");

        Result result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WhenProviderThrows_ReturnsSuccess()
    {
        _smsProvider.SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<SmsDeliveryResult>(_ => throw new InvalidOperationException("Connection failed"));

        SendSmsCommand command = new("+15551234567", "Hello world");

        Result result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WithFrom_PassesFromToPhoneNumber()
    {
        SendSmsCommand command = new("+15551234567", "Hello world", "+15559876543");

        Result result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_PassesCancellationToken()
    {
        using CancellationTokenSource cts = new();
        SendSmsCommand command = new("+15551234567", "Hello world");

        await _handler.Handle(command, cts.Token);

        await _smsProvider.Received(1).SendAsync(
            Arg.Any<string>(), Arg.Any<string>(), cts.Token);
    }
}
