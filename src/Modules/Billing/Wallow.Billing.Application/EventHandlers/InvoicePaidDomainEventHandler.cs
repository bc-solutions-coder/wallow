using Wallow.Billing.Application.Interfaces;
using Wallow.Billing.Domain.Entities;
using Wallow.Billing.Domain.Events;
using Wallow.Billing.Domain.Identity;
using Wallow.Shared.Contracts.Billing.Events;
using Wallow.Shared.Contracts.Identity;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace Wallow.Billing.Application.EventHandlers;

public sealed partial class InvoicePaidDomainEventHandler
{
    public static async Task HandleAsync(
        InvoicePaidDomainEvent domainEvent,
        IInvoiceRepository invoiceRepository,
        IUserQueryService userQueryService,
        IMessageBus bus,
        ILogger<InvoicePaidDomainEventHandler> logger,
        CancellationToken cancellationToken)
    {
        LogHandlingInvoicePaid(logger, domainEvent.InvoiceId);

        Invoice? invoice = await invoiceRepository.GetByIdAsync(
            InvoiceId.Create(domainEvent.InvoiceId), cancellationToken);

        if (invoice is null)
        {
            LogInvoiceNotFound(logger, domainEvent.InvoiceId);
            return;
        }

        string userEmail = await userQueryService.GetUserEmailAsync(invoice.UserId, cancellationToken);

        await bus.PublishAsync(new InvoicePaidEvent
        {
            InvoiceId = domainEvent.InvoiceId,
            TenantId = invoice.TenantId.Value,
            PaymentId = domainEvent.PaymentId,
            UserId = invoice.UserId,
            UserEmail = userEmail,
            InvoiceNumber = invoice.InvoiceNumber,
            Amount = invoice.TotalAmount.Amount,
            Currency = invoice.TotalAmount.Currency,
            PaidAt = domainEvent.PaidAt
        });

        LogPublishedInvoicePaid(logger, domainEvent.InvoiceId);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Handling InvoicePaidDomainEvent for Invoice {InvoiceId}")]
    private static partial void LogHandlingInvoicePaid(ILogger logger, Guid invoiceId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Invoice {InvoiceId} not found")]
    private static partial void LogInvoiceNotFound(ILogger logger, Guid invoiceId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Published InvoicePaidEvent for Invoice {InvoiceId}")]
    private static partial void LogPublishedInvoicePaid(ILogger logger, Guid invoiceId);
}
