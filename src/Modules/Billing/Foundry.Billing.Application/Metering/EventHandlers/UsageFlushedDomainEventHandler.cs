using Foundry.Billing.Domain.Metering.Events;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace Foundry.Billing.Application.Metering.EventHandlers;

/// <summary>
/// Bridges the UsageFlushed domain event to the integration event for cross-module communication.
/// Allows the Billing module to process usage for invoicing.
/// </summary>
public sealed partial class UsageFlushedDomainEventHandler
{
    public static async Task HandleAsync(
        UsageFlushedEvent domainEvent,
        IMessageBus bus,
        ILogger<UsageFlushedDomainEventHandler> logger)
    {
        LogHandlingUsageFlushed(logger, domainEvent.FlushedAt, domainEvent.RecordCount);

        // Publish integration event for Billing module
        await bus.PublishAsync(new Foundry.Shared.Contracts.Metering.Events.UsageFlushedEvent
        {
            FlushedAt = domainEvent.FlushedAt,
            RecordCount = domainEvent.RecordCount
        });

        LogPublishedUsageFlushed(logger);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Handling UsageFlushedEvent at {FlushedAt} with {RecordCount} records")]
    private static partial void LogHandlingUsageFlushed(ILogger logger, DateTime flushedAt, int recordCount);

    [LoggerMessage(Level = LogLevel.Information, Message = "Published UsageFlushedEvent integration event")]
    private static partial void LogPublishedUsageFlushed(ILogger logger);
}
