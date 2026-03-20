using Wallow.Billing.Application.Interfaces;
using Wallow.Billing.Domain.Entities;
using Wallow.Billing.Domain.Events;
using Wallow.Billing.Domain.Identity;
using Wallow.Shared.Contracts.Billing.Events;
using Wallow.Shared.Contracts.Identity;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace Wallow.Billing.Application.EventHandlers;

public sealed partial class InvoiceOverdueDomainEventHandler
{
    public static async Task HandleAsync(
        InvoiceOverdueDomainEvent domainEvent,
        IInvoiceRepository invoiceRepository,
        IUserQueryService userQueryService,
        IMessageBus bus,
        ILogger<InvoiceOverdueDomainEventHandler> logger,
        CancellationToken cancellationToken)
    {
        LogHandlingInvoiceOverdue(logger, domainEvent.InvoiceId);

        Invoice? invoice = await invoiceRepository.GetByIdAsync(
            InvoiceId.Create(domainEvent.InvoiceId), cancellationToken);

        if (invoice is null)
        {
            LogInvoiceNotFound(logger, domainEvent.InvoiceId);
            return;
        }

        string userEmail = await userQueryService.GetUserEmailAsync(domainEvent.UserId, cancellationToken);

        await bus.PublishAsync(new InvoiceOverdueEvent
        {
            InvoiceId = domainEvent.InvoiceId,
            TenantId = invoice.TenantId.Value,
            UserId = domainEvent.UserId,
            UserEmail = userEmail,
            InvoiceNumber = invoice.InvoiceNumber,
            Amount = invoice.TotalAmount.Amount,
            Currency = invoice.TotalAmount.Currency,
            DueDate = domainEvent.DueDate
        });

        LogPublishedInvoiceOverdue(logger, domainEvent.InvoiceId);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Handling InvoiceOverdueDomainEvent for Invoice {InvoiceId}")]
    private static partial void LogHandlingInvoiceOverdue(ILogger logger, Guid invoiceId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Invoice {InvoiceId} not found")]
    private static partial void LogInvoiceNotFound(ILogger logger, Guid invoiceId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Published InvoiceOverdueEvent for Invoice {InvoiceId}")]
    private static partial void LogPublishedInvoiceOverdue(ILogger logger, Guid invoiceId);
}
