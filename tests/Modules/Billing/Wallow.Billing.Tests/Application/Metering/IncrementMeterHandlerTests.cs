using Microsoft.Extensions.Logging;
using NSubstitute.Core;
using Wallow.Billing.Application.Metering.Commands.IncrementMeter;
using Wallow.Billing.Application.Metering.Services;
using Wallow.Tests.Common.Helpers;

namespace Wallow.Billing.Tests.Application.Metering;

public class IncrementMeterHandlerTests
{
    private readonly IMeteringService _meteringService;
    private readonly ILogger<IncrementMeterHandler> _logger;
    private readonly IncrementMeterHandler _handler;

    public IncrementMeterHandlerTests()
    {
        _meteringService = Substitute.For<IMeteringService>();
        _logger = Substitute.For<ILogger<IncrementMeterHandler>>();
        _logger.IsEnabled(Arg.Any<LogLevel>()).Returns(true);
        _handler = new IncrementMeterHandler(_meteringService, _logger);
    }

    [Fact]
    public async Task Handle_WithValidCommand_CallsIncrementAsync()
    {
        IncrementMeterCommand command = new("api.calls", 1);

        await _handler.Handle(command);

        await _meteringService.Received(1).IncrementAsync("api.calls", 1, null);
    }

    [Fact]
    public async Task Handle_WithCustomValue_PassesValueToService()
    {
        IncrementMeterCommand command = new("storage.bytes", 512);

        await _handler.Handle(command);

        await _meteringService.Received(1).IncrementAsync("storage.bytes", 512, null);
    }

    [Fact]
    public async Task Handle_WhenServiceThrows_DoesNotPropagate()
    {
        _meteringService.IncrementAsync(Arg.Any<string>(), Arg.Any<decimal>(), null)
            .Returns(Task.FromException(new InvalidOperationException("Redis down")));

        IncrementMeterCommand command = new("api.calls");

        Exception? exception = await Record.ExceptionAsync(() => _handler.Handle(command));

        exception.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WhenServiceThrows_LogsWarning()
    {
        _meteringService.IncrementAsync(Arg.Any<string>(), Arg.Any<decimal>(), null)
            .Returns(Task.FromException(new InvalidOperationException("Redis down")));

        IncrementMeterCommand command = new("api.calls");

        await _handler.Handle(command);

        IEnumerable<ICall> logCalls = _logger.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == "Log");
        logCalls.Should().ContainSingle();
        object?[] args = logCalls.Single().GetArguments();
        ((LogLevel)args[0]!).Should().Be(LogLevel.Warning);
    }

    [Fact]
    public async Task Handle_WithDefaultValue_UsesOne()
    {
        IncrementMeterCommand command = new("api.calls");

        await _handler.Handle(command);

        await _meteringService.Received(1).IncrementAsync("api.calls", 1, null);
    }

    [Fact]
    public async Task Handle_WhenServiceThrows_LogsWarningContainingMeterCode()
    {
        _meteringService.IncrementAsync(Arg.Any<string>(), Arg.Any<decimal>(), null)
            .Returns(Task.FromException(new InvalidOperationException("Redis down")));

        IncrementMeterCommand command = new("storage.quota");

        await _handler.Handle(command);

        _logger.ShouldHaveLoggedMessage("storage.quota");
    }

    [Fact]
    public async Task Handle_WithCustomValue_CallsServiceWithCorrectValue()
    {
        IncrementMeterCommand command = new("api.calls", 7);

        await _handler.Handle(command);

        await _meteringService.Received(1).IncrementAsync("api.calls", 7, null);
    }
}
