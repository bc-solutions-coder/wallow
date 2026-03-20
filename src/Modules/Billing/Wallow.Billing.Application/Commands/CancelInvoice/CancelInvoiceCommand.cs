namespace Wallow.Billing.Application.Commands.CancelInvoice;

public sealed record CancelInvoiceCommand(
    Guid InvoiceId,
    Guid CancelledByUserId);
