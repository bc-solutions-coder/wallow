namespace Wallow.Billing.Application.Queries.GetAllSubscriptions;

public sealed record GetAllSubscriptionsQuery(int Skip = 0, int Take = 50);
