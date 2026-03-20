namespace Wallow.Billing.Application.DTOs;

public sealed record InvoiceLineItemDto(
    Guid Id,
    string Description,
    decimal UnitPrice,
    string Currency,
    int Quantity,
    decimal LineTotal);
