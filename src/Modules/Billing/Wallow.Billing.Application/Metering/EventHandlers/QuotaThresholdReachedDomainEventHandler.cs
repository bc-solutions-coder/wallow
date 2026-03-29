using Microsoft.Extensions.Logging;
using Wallow.Billing.Domain.Metering.Events;
using Wolverine;

namespace Wallow.Billing.Application.Metering.EventHandlers;

/// <summary>
/// Bridges the QuotaThresholdReached domain event to the integration event for cross-module communication.
/// Allows the Notifications module to alert users about quota usage.
/// </summary>
public sealed partial class QuotaThresholdReachedDomainEventHandler
{
    public static async Task HandleAsync(
        QuotaThresholdReachedEvent domainEvent,
        IMessageBus bus,
        ILogger<QuotaThresholdReachedDomainEventHandler> logger)
    {
        LogHandlingQuotaThresholdReached(logger, domainEvent.TenantId, domainEvent.MeterCode, domainEvent.PercentUsed);

        // Publish integration event for Notifications module
        await bus.PublishAsync(new Wallow.Shared.Contracts.Metering.Events.QuotaThresholdReachedEvent
        {
            TenantId = domainEvent.TenantId,
            MeterCode = domainEvent.MeterCode,
            MeterDisplayName = domainEvent.MeterDisplayName,
            CurrentUsage = domainEvent.CurrentUsage,
            Limit = domainEvent.Limit,
            PercentUsed = domainEvent.PercentUsed,
            Period = "monthly" // Default to monthly, could be enriched from quota definition
        });

        LogPublishedQuotaThresholdReached(logger, domainEvent.TenantId);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Handling QuotaThresholdReachedEvent for tenant {TenantId}, meter {MeterCode} at {PercentUsed}%")]
    private static partial void LogHandlingQuotaThresholdReached(ILogger logger, Guid tenantId, string meterCode, decimal percentUsed);

    [LoggerMessage(Level = LogLevel.Information, Message = "Published QuotaThresholdReachedEvent integration event for tenant {TenantId}")]
    private static partial void LogPublishedQuotaThresholdReached(ILogger logger, Guid tenantId);
}
