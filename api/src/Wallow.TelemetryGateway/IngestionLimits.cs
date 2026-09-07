using System.Threading.RateLimiting;

namespace Wallow.TelemetryGateway;

internal static class IngestionLimits
{
    public static PartitionedRateLimiter<Guid> Create()
    {
        return PartitionedRateLimiter.CreateChained(
            PartitionedRateLimiter.Create<Guid, string>(_ => RateLimitPartition.GetConcurrencyLimiter("global", _ => new ConcurrencyLimiterOptions
            {
                PermitLimit = 16,
                QueueLimit = 0,
            })),
            PartitionedRateLimiter.Create<Guid, Guid>(registration => RateLimitPartition.GetTokenBucketLimiter(registration, _ => new TokenBucketRateLimiterOptions
            {
                TokenLimit = 20,
                TokensPerPeriod = 10,
                ReplenishmentPeriod = TimeSpan.FromSeconds(1),
                AutoReplenishment = true,
                QueueLimit = 0,
            })),
            PartitionedRateLimiter.Create<Guid, string>(_ => RateLimitPartition.GetTokenBucketLimiter("global", _ => new TokenBucketRateLimiterOptions
            {
                TokenLimit = 100,
                TokensPerPeriod = 50,
                ReplenishmentPeriod = TimeSpan.FromSeconds(1),
                AutoReplenishment = true,
                QueueLimit = 0,
            })));
    }
}
