using Microsoft.Extensions.Logging;
using Wallow.Billing.Application.Interfaces;
using Wallow.Billing.Domain.Entities;
using Wallow.Shared.Contracts.Billing;

namespace Wallow.Billing.Infrastructure.Services;

public sealed partial class SubscriptionQueryService(
    ISubscriptionRepository subscriptionRepository,
    ILogger<SubscriptionQueryService> logger) : ISubscriptionQueryService
{

    public async Task<string?> GetActivePlanCodeAsync(Guid tenantId, CancellationToken ct = default)
    {
        try
        {
            Subscription? subscription = await subscriptionRepository.GetActiveByUserIdAsync(tenantId, ct);

            if (subscription is null)
            {
                LogNoActiveSubscription(logger, tenantId);
                return null;
            }

            return subscription.PlanName;
        }
        catch (Exception ex)
        {
            LogGetActivePlanFailed(logger, ex, tenantId);
            return null;
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "No active subscription found for tenant {TenantId}")]
    private static partial void LogNoActiveSubscription(ILogger logger, Guid tenantId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to get active plan code for tenant {TenantId}")]
    private static partial void LogGetActivePlanFailed(ILogger logger, Exception ex, Guid tenantId);
}
