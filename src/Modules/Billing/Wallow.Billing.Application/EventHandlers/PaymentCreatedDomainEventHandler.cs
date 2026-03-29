using Microsoft.Extensions.Logging;
using Wallow.Billing.Domain.Events;
using Wallow.Shared.Contracts.Identity;
using Wallow.Shared.Kernel.MultiTenancy;
using Wolverine;

namespace Wallow.Billing.Application.EventHandlers;

public sealed partial class PaymentCreatedDomainEventHandler
{
    public static async Task HandleAsync(
        PaymentCreatedDomainEvent domainEvent,
        IMessageBus bus,
        ITenantContext tenantContext,
        IUserQueryService userQueryService,
        TimeProvider timeProvider,
        ILogger<PaymentCreatedDomainEventHandler> logger,
        CancellationToken cancellationToken)
    {
        LogHandlingPaymentCreated(logger, domainEvent.PaymentId);

        string userEmail = await userQueryService.GetUserEmailAsync(domainEvent.UserId, cancellationToken);

        await bus.PublishAsync(new Wallow.Shared.Contracts.Billing.Events.PaymentReceivedEvent
        {
            PaymentId = domainEvent.PaymentId,
            TenantId = tenantContext.TenantId.Value,
            InvoiceId = domainEvent.InvoiceId,
            UserId = domainEvent.UserId,
            UserEmail = userEmail,
            Amount = domainEvent.Amount,
            Currency = domainEvent.Currency,
            PaymentMethod = domainEvent.PaymentMethod,
            PaidAt = timeProvider.GetUtcNow().UtcDateTime
        });

        LogPublishedPaymentReceived(logger, domainEvent.PaymentId);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Handling PaymentCreatedDomainEvent for Payment {PaymentId}")]
    private static partial void LogHandlingPaymentCreated(ILogger logger, Guid paymentId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Published PaymentReceivedEvent for Payment {PaymentId}")]
    private static partial void LogPublishedPaymentReceived(ILogger logger, Guid paymentId);
}
