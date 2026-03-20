namespace Wallow.Billing.Application.Queries.GetPaymentsByInvoiceId;

public sealed record GetPaymentsByInvoiceIdQuery(Guid InvoiceId);
