namespace Wallow.Billing.Domain.Enums;

/// <summary>
/// Represents the lifecycle status of an invoice.
/// </summary>
public enum InvoiceStatus
{
    Draft = 0,
    Issued = 1,
    Paid = 2,
    Overdue = 3,
    Cancelled = 4
}
