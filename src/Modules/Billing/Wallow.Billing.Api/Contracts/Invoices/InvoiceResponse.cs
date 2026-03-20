namespace Wallow.Billing.Api.Contracts.Invoices;

public sealed record InvoiceResponse(
    Guid Id,
    Guid UserId,
    string InvoiceNumber,
    string Status,
    decimal TotalAmount,
    string Currency,
    DateTime? DueDate,
    DateTime? PaidAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    IReadOnlyList<InvoiceLineItemResponse> LineItems);

public sealed record InvoiceLineItemResponse(
    Guid Id,
    string Description,
    decimal UnitPrice,
    string Currency,
    int Quantity,
    decimal LineTotal);
