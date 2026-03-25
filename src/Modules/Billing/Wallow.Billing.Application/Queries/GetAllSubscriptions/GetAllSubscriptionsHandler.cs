using Wallow.Billing.Application.DTOs;
using Wallow.Billing.Application.Interfaces;
using Wallow.Billing.Application.Mappings;
using Wallow.Billing.Domain.Entities;
using Wallow.Shared.Kernel.Pagination;
using Wallow.Shared.Kernel.Results;

namespace Wallow.Billing.Application.Queries.GetAllSubscriptions;

public sealed class GetAllSubscriptionsHandler(ISubscriptionRepository subscriptionRepository)
{
    public async Task<Result<PagedResult<SubscriptionDto>>> Handle(
        GetAllSubscriptionsQuery query,
        CancellationToken cancellationToken)
    {
        int totalCount = await subscriptionRepository.CountAllAsync(cancellationToken);
        IReadOnlyList<Subscription> subscriptions = await subscriptionRepository.GetAllAsync(query.Skip, query.Take, cancellationToken);
        List<SubscriptionDto> dtos = subscriptions.Select(s => s.ToDto()).ToList();
        int page = (query.Skip / query.Take) + 1;
        return Result.Success(new PagedResult<SubscriptionDto>(dtos, totalCount, page, query.Take));
    }
}
