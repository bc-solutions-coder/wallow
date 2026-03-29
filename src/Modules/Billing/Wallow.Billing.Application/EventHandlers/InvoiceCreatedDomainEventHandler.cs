using Microsoft.Extensions.Logging;
using Wallow.Billing.Application.Interfaces;
using Wallow.Billing.Application.Telemetry;
using Wallow.Billing.Domain.Entities;
using Wallow.Billing.Domain.Events;
using Wallow.Billing.Domain.Identity;
using Wallow.Shared.Contracts.Identity;
using Wolverine;

namespace Wallow.Billing.Application.EventHandlers;

public sealed partial class InvoiceCreatedDomainEventHandler
{
    private const int DefaultDueDateDays = 30;

    public static async Task HandleAsync(
        InvoiceCreatedDomainEvent domainEvent,
        IInvoiceRepository invoiceRepository,
        IMessageBus bus,
        IUserQueryService userQueryService,
        TimeProvider timeProvider,
        ILogger<InvoiceCreatedDomainEventHandler> logger,
        CancellationToken cancellationToken)
    {
        LogHandlingInvoiceCreated(logger, domainEvent.InvoiceId);

        // Enrich with additional data
        Invoice? invoice = await invoiceRepository.GetByIdAsync(
            InvoiceId.Create(domainEvent.InvoiceId), cancellationToken);

        string userEmail = await userQueryService.GetUserEmailAsync(domainEvent.UserId, cancellationToken);

        // Publish integration event for other modules
        await bus.PublishAsync(new Wallow.Shared.Contracts.Billing.Events.InvoiceCreatedEvent
        {
            InvoiceId = domainEvent.InvoiceId,
            TenantId = invoice?.TenantId.Value ?? Guid.Empty,
            UserId = domainEvent.UserId,
            UserEmail = userEmail,
            InvoiceNumber = invoice?.InvoiceNumber ?? string.Empty,
            Amount = domainEvent.TotalAmount,
            Currency = domainEvent.Currency,
            DueDate = invoice?.DueDate ?? timeProvider.GetUtcNow().UtcDateTime.AddDays(DefaultDueDateDays)
        });

        string status = invoice?.Status.ToString() ?? "Unknown";
        string currency = domainEvent.Currency;

        BillingModuleTelemetry.InvoicesCreatedTotal.Add(1,
            new KeyValuePair<string, object?>("status", status),
            new KeyValuePair<string, object?>("currency", currency));

        BillingModuleTelemetry.InvoiceAmount.Record((double)domainEvent.TotalAmount,
            new KeyValuePair<string, object?>("status", status),
            new KeyValuePair<string, object?>("currency", currency));

        LogPublishedInvoiceCreated(logger, domainEvent.InvoiceId);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Handling InvoiceCreatedDomainEvent for Invoice {InvoiceId}")]
    private static partial void LogHandlingInvoiceCreated(ILogger logger, Guid invoiceId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Published InvoiceCreatedEvent for Invoice {InvoiceId}")]
    private static partial void LogPublishedInvoiceCreated(ILogger logger, Guid invoiceId);
}
