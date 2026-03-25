namespace Wallow.Billing.Application.Queries.GetAllPayments;

public sealed record GetAllPaymentsQuery(int Skip = 0, int Take = 50);
