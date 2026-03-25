namespace Wallow.Billing.Application.Queries.GetAllInvoices;

public sealed record GetAllInvoicesQuery(int Skip = 0, int Take = 50);
