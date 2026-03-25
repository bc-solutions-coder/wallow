using Microsoft.Extensions.Logging;
using NSubstitute.Core;
using Wallow.Billing.Application.Metering.EventHandlers;
using Wallow.Billing.Domain.Metering.Events;
using Wolverine;

namespace Wallow.Billing.Tests.Application.Metering;

public class UsageFlushedDomainEventHandlerTests
{
    private readonly IMessageBus _messageBus;
    private readonly ILogger<UsageFlushedDomainEventHandler> _logger;

    public UsageFlushedDomainEventHandlerTests()
    {
        _messageBus = Substitute.For<IMessageBus>();
        _logger = Substitute.For<ILogger<UsageFlushedDomainEventHandler>>();
        _logger.IsEnabled(Arg.Any<LogLevel>()).Returns(true);
    }

    [Fact]
    public async Task HandleAsync_PublishesIntegrationEvent()
    {
        DateTime flushedAt = DateTime.UtcNow;
        UsageFlushedEvent domainEvent = new(FlushedAt: flushedAt, RecordCount: 42);

        await UsageFlushedDomainEventHandler.HandleAsync(
            domainEvent,
            _messageBus,
            _logger);

        await _messageBus.Received(1).PublishAsync(
            Arg.Is<Wallow.Shared.Contracts.Metering.Events.UsageFlushedEvent>(e =>
                e.FlushedAt == flushedAt &&
                e.RecordCount == 42));
    }

    [Fact]
    public async Task HandleAsync_LogsInformation()
    {
        DateTime flushedAt = DateTime.UtcNow;
        UsageFlushedEvent domainEvent = new(FlushedAt: flushedAt, RecordCount: 10);

        await UsageFlushedDomainEventHandler.HandleAsync(
            domainEvent,
            _messageBus,
            _logger);

        List<ICall> calls = _logger.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == "Log")
            .Where(c => c.GetArguments()[0] is LogLevel level && level == LogLevel.Information)
            .ToList();
        calls.Should().HaveCountGreaterThanOrEqualTo(1, "expected at least one Information log call");
    }

    [Fact]
    public async Task HandleAsync_WithZeroRecords_StillPublishes()
    {
        DateTime flushedAt = DateTime.UtcNow;
        UsageFlushedEvent domainEvent = new(FlushedAt: flushedAt, RecordCount: 0);

        await UsageFlushedDomainEventHandler.HandleAsync(
            domainEvent,
            _messageBus,
            _logger);

        await _messageBus.Received(1).PublishAsync(
            Arg.Is<Wallow.Shared.Contracts.Metering.Events.UsageFlushedEvent>(e =>
                e.RecordCount == 0));
    }
}
