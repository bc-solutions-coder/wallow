using Foundry.Billing.Domain.Events;
using Foundry.Shared.Kernel.MultiTenancy;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace Foundry.Billing.Application.EventHandlers;

public sealed partial class PaymentCreatedDomainEventHandler
{
    public static async Task HandleAsync(
        PaymentCreatedDomainEvent domainEvent,
        IMessageBus bus,
        ITenantContext tenantContext,
        ILogger<PaymentCreatedDomainEventHandler> logger,
        CancellationToken _)
    {
        LogHandlingPaymentCreated(logger, domainEvent.PaymentId);

        await bus.PublishAsync(new Foundry.Shared.Contracts.Billing.Events.PaymentReceivedEvent
        {
            PaymentId = domainEvent.PaymentId,
            TenantId = tenantContext.TenantId.Value,
            InvoiceId = domainEvent.InvoiceId,
            UserId = domainEvent.UserId,
            UserEmail = string.Empty,
            Amount = domainEvent.Amount,
            Currency = domainEvent.Currency,
            PaymentMethod = string.Empty,
            PaidAt = DateTime.UtcNow
        });

        LogPublishedPaymentReceived(logger, domainEvent.PaymentId);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Handling PaymentCreatedDomainEvent for Payment {PaymentId}")]
    private static partial void LogHandlingPaymentCreated(ILogger logger, Guid paymentId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Published PaymentReceivedEvent for Payment {PaymentId}")]
    private static partial void LogPublishedPaymentReceived(ILogger logger, Guid paymentId);
}
