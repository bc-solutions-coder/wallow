namespace Wallow.Billing.Api.Contracts.Invoices;

public sealed record AddLineItemRequest(
    string Description,
    decimal UnitPrice,
    int Quantity);
