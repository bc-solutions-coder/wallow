using Foundry.Communications.Application.Channels.Sms.Commands.SendSms;
using Foundry.Communications.Application.Channels.Sms.EventHandlers;
using Foundry.Shared.Contracts.Communications.Sms.Events;
using Foundry.Shared.Kernel.Results;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace Foundry.Communications.Tests.Application.Channels.Sms;

public class SendSmsRequestedEventHandlerTests
{
    private readonly IMessageBus _bus;
    private readonly ILogger<SendSmsRequestedEvent> _logger;

    public SendSmsRequestedEventHandlerTests()
    {
        _bus = Substitute.For<IMessageBus>();
        _logger = Substitute.For<ILogger<SendSmsRequestedEvent>>();
        _logger.IsEnabled(Arg.Any<LogLevel>()).Returns(true);

        _bus.InvokeAsync<Result>(Arg.Any<SendSmsCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());
    }

    [Fact]
    public async Task HandleAsync_InvokesCommandViaBus()
    {
        SendSmsRequestedEvent evt = new()
        {
            TenantId = Guid.NewGuid(),
            To = "+15551234567",
            Body = "Hello world",
            SourceModule = "Billing"
        };

        await SendSmsRequestedEventHandler.HandleAsync(evt, _bus, _logger, CancellationToken.None);

        await _bus.Received(1).InvokeAsync<Result>(
            Arg.Is<SendSmsCommand>(c => c.To == "+15551234567" && c.Body == "Hello world"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WhenBusThrows_RethrowsException()
    {
        _bus.InvokeAsync<Result>(Arg.Any<SendSmsCommand>(), Arg.Any<CancellationToken>())
            .Returns<Result>(_ => throw new InvalidOperationException("Handler failed"));

        SendSmsRequestedEvent evt = new()
        {
            TenantId = Guid.NewGuid(),
            To = "+15551234567",
            Body = "Hello world",
            SourceModule = "Test"
        };

        Func<Task> act = () => SendSmsRequestedEventHandler.HandleAsync(
            evt, _bus, _logger, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Handler failed");
    }

    [Fact]
    public async Task HandleAsync_WithNullSourceModule_UsesUnknown()
    {
        SendSmsRequestedEvent evt = new()
        {
            TenantId = Guid.NewGuid(),
            To = "+15551234567",
            Body = "Hello world",
            SourceModule = null
        };

        await SendSmsRequestedEventHandler.HandleAsync(evt, _bus, _logger, CancellationToken.None);

        await _bus.Received(1).InvokeAsync<Result>(
            Arg.Any<SendSmsCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_PassesCancellationToken()
    {
        using CancellationTokenSource cts = new();
        SendSmsRequestedEvent evt = new()
        {
            TenantId = Guid.NewGuid(),
            To = "+15551234567",
            Body = "Hello world"
        };

        await SendSmsRequestedEventHandler.HandleAsync(evt, _bus, _logger, cts.Token);

        await _bus.Received(1).InvokeAsync<Result>(
            Arg.Any<SendSmsCommand>(), cts.Token);
    }
}
