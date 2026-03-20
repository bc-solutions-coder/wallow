namespace Wallow.Billing.Application.Commands.CreateInvoice;

public sealed record CreateInvoiceCommand(
    Guid UserId,
    string InvoiceNumber,
    string Currency,
    DateTime? DueDate,
    Dictionary<string, object>? CustomFields = null);
