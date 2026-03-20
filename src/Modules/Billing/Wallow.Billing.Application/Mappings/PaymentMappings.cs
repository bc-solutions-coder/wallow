using Wallow.Billing.Application.DTOs;
using Wallow.Billing.Domain.Entities;

namespace Wallow.Billing.Application.Mappings;

public static class PaymentMappings
{
    public static PaymentDto ToDto(this Payment payment)
    {
        return new PaymentDto(
            Id: payment.Id.Value,
            InvoiceId: payment.InvoiceId.Value,
            UserId: payment.UserId,
            Amount: payment.Amount.Amount,
            Currency: payment.Amount.Currency,
            Method: payment.Method.ToString(),
            Status: payment.Status.ToString(),
            TransactionReference: payment.TransactionReference,
            FailureReason: payment.FailureReason,
            CompletedAt: payment.CompletedAt,
            CreatedAt: payment.CreatedAt,
            UpdatedAt: payment.UpdatedAt,
            CustomFields: payment.CustomFields);
    }
}
