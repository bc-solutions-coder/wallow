using Wallow.Billing.Application.Metering.EventHandlers;
using Wallow.Billing.Domain.Metering.Events;
using Microsoft.Extensions.Logging;
using NSubstitute.Core;
using Wolverine;
using static Wallow.Tests.Common.Helpers.LoggerAssertionExtensions;

namespace Wallow.Billing.Tests.Application.Metering;

public class QuotaThresholdReachedDomainEventHandlerTests
{
    private readonly IMessageBus _messageBus;
    private readonly ILogger<QuotaThresholdReachedDomainEventHandler> _logger;

    public QuotaThresholdReachedDomainEventHandlerTests()
    {
        _messageBus = Substitute.For<IMessageBus>();
        _logger = Substitute.For<ILogger<QuotaThresholdReachedDomainEventHandler>>();
        _logger.IsEnabled(Arg.Any<LogLevel>()).Returns(true);
    }

    [Fact]
    public async Task HandleAsync_PublishesIntegrationEvent()
    {
        Guid tenantId = Guid.NewGuid();
        QuotaThresholdReachedEvent domainEvent = new(
            TenantId: tenantId,
            MeterCode: "api.calls",
            MeterDisplayName: "API Calls",
            CurrentUsage: 900,
            Limit: 1000,
            PercentUsed: 90);

        await QuotaThresholdReachedDomainEventHandler.HandleAsync(
            domainEvent,
            _messageBus,
            _logger);

        await _messageBus.Received(1).PublishAsync(
            Arg.Is<Wallow.Shared.Contracts.Metering.Events.QuotaThresholdReachedEvent>(e =>
                e.TenantId == tenantId &&
                e.MeterCode == "api.calls" &&
                e.MeterDisplayName == "API Calls" &&
                e.CurrentUsage == 900 &&
                e.Limit == 1000 &&
                e.PercentUsed == 90 &&
                e.Period == "monthly"));
    }

    [Fact]
    public async Task HandleAsync_LogsInformation()
    {
        Guid tenantId = Guid.NewGuid();
        QuotaThresholdReachedEvent domainEvent = new(
            TenantId: tenantId,
            MeterCode: "api.calls",
            MeterDisplayName: "API Calls",
            CurrentUsage: 800,
            Limit: 1000,
            PercentUsed: 80);

        await QuotaThresholdReachedDomainEventHandler.HandleAsync(
            domainEvent,
            _messageBus,
            _logger);

        List<ICall> calls = _logger.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == "Log")
            .Where(c => c.GetArguments()[0] is LogLevel level && level == LogLevel.Information)
            .Where(c => LogMessageContains(c, "api.calls"))
            .ToList();
        calls.Should().HaveCountGreaterThanOrEqualTo(1, "expected at least one Information log call containing the meter code");
    }
}
