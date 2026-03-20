namespace Wallow.Billing.Api.Contracts.Invoices;

public sealed record CreateInvoiceRequest(
    string InvoiceNumber,
    string Currency,
    DateTime? DueDate,
    Guid? UserId = null);
