namespace Wallow.Billing.Application.Commands.AddLineItem;

public sealed record AddLineItemCommand(
    Guid InvoiceId,
    string Description,
    decimal UnitPrice,
    int Quantity,
    Guid UpdatedByUserId);
