namespace Wallow.Billing.Domain.Enums;

/// <summary>
/// Represents the status of a payment.
/// </summary>
public enum PaymentStatus
{
    Pending = 0,
    Completed = 1,
    Failed = 2,
    Refunded = 3
}
