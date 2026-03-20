namespace Wallow.Billing.Application.Commands.IssueInvoice;

public sealed record IssueInvoiceCommand(
    Guid InvoiceId,
    Guid IssuedByUserId);
