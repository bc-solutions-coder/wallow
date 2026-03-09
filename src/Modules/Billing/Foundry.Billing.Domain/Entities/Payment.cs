using Foundry.Billing.Domain.Enums;
using Foundry.Billing.Domain.Events;
using Foundry.Billing.Domain.Exceptions;
using Foundry.Billing.Domain.Identity;
using Foundry.Billing.Domain.ValueObjects;
using Foundry.Shared.Kernel.CustomFields;
using Foundry.Shared.Kernel.Domain;
using Foundry.Shared.Kernel.Identity;
using Foundry.Shared.Kernel.MultiTenancy;

namespace Foundry.Billing.Domain.Entities;

public sealed class Payment : AggregateRoot<PaymentId>, ITenantScoped, IHasCustomFields
{
    public TenantId TenantId { get; init; }
    public InvoiceId InvoiceId { get; init; }
    public Guid UserId { get; init; }
    public Money Amount { get; private set; } = null!;
    public PaymentMethod Method { get; private set; }
    public PaymentStatus Status { get; private set; }
    public string? TransactionReference { get; private set; }
    public string? FailureReason { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public Dictionary<string, object>? CustomFields { get; private set; }

    public void SetCustomFields(Dictionary<string, object>? customFields)
    {
        CustomFields = customFields;
    }

    // ReSharper disable once UnusedMember.Local
    private Payment() { } // EF Core

    private Payment(
        InvoiceId invoiceId,
        Guid userId,
        Money amount,
        PaymentMethod method,
        Guid createdByUserId,
        TimeProvider timeProvider)
    {
        Id = PaymentId.New();
        InvoiceId = invoiceId;
        UserId = userId;
        Amount = amount;
        Method = method;
        Status = PaymentStatus.Pending;
        SetCreated(timeProvider.GetUtcNow(), createdByUserId);
    }

    public static Payment Create(
        InvoiceId invoiceId,
        Guid userId,
        Money amount,
        PaymentMethod method,
        Guid createdByUserId,
        TimeProvider timeProvider,
        Dictionary<string, object>? customFields = null)
    {
        if (userId == Guid.Empty)
        {
            throw new BusinessRuleException("Billing.UserIdRequired", "User ID is required");
        }

        if (invoiceId.Value == Guid.Empty)
        {
            throw new BusinessRuleException("Billing.InvoiceIdRequired", "Invoice ID is required");
        }

        if (amount.Amount <= 0)
        {
            throw new InvalidPaymentException("Payment amount must be greater than zero");
        }

        Payment payment = new(invoiceId, userId, amount, method, createdByUserId, timeProvider) { CustomFields = customFields };

        payment.RaiseDomainEvent(new PaymentCreatedDomainEvent(
            payment.Id.Value,
            invoiceId.Value,
            amount.Amount,
            amount.Currency,
            userId));

        return payment;
    }

    public void Complete(string transactionReference, Guid updatedByUserId, TimeProvider timeProvider)
    {
        if (Status != PaymentStatus.Pending)
        {
            throw new InvalidPaymentException(
                $"Cannot complete payment in {Status} status. Only Pending payments can be completed.");
        }

        Status = PaymentStatus.Completed;
        TransactionReference = transactionReference;
        CompletedAt = timeProvider.GetUtcNow().UtcDateTime;
        SetUpdated(timeProvider.GetUtcNow(), updatedByUserId);
    }

    public void Fail(string reason, Guid updatedByUserId, TimeProvider timeProvider)
    {
        if (Status != PaymentStatus.Pending)
        {
            throw new InvalidPaymentException(
                $"Cannot fail payment in {Status} status. Only Pending payments can be marked as failed.");
        }

        Status = PaymentStatus.Failed;
        FailureReason = reason;
        SetUpdated(timeProvider.GetUtcNow(), updatedByUserId);

        RaiseDomainEvent(new PaymentFailedDomainEvent(
            Id.Value,
            InvoiceId.Value,
            reason,
            UserId));
    }

    public void Refund(Guid updatedByUserId, TimeProvider timeProvider)
    {
        if (Status != PaymentStatus.Completed)
        {
            throw new InvalidPaymentException(
                $"Cannot refund payment in {Status} status. Only Completed payments can be refunded.");
        }

        Status = PaymentStatus.Refunded;
        SetUpdated(timeProvider.GetUtcNow(), updatedByUserId);
    }
}
