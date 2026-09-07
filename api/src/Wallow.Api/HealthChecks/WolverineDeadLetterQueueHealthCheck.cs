using System.Diagnostics.Metrics;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Wallow.Shared.Kernel;
using Wolverine.Logging;
using Wolverine.Persistence.Durability;

namespace Wallow.Api.HealthChecks;

/// <summary>
/// Reports a nonempty dead-letter queue as Degraded and records its depth on Wallow.Messaging.
/// Storage-query failures are Unhealthy. Host registration omits the ready tag.
/// </summary>
internal sealed class WolverineDeadLetterQueueHealthCheck(IMessageStore messageStore) : IHealthCheck
{
    private static readonly Meter _messagingMeter = Diagnostics.CreateMeter("Messaging");

    private static readonly Gauge<long> _deadLetterQueueDepth = _messagingMeter.CreateGauge<long>(
        "wallow.messaging.dead_letter_queue.depth",
        description: "Number of envelopes currently in the Wolverine dead-letter queue");

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            PersistedCounts counts = await messageStore.Admin.FetchCountsAsync();
            _deadLetterQueueDepth.Record(counts.DeadLetter);

            return counts.DeadLetter == 0
                ? HealthCheckResult.Healthy("Dead-letter queue is empty.")
                : HealthCheckResult.Degraded(
                    $"{counts.DeadLetter} envelope(s) in the Wolverine dead-letter queue.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Could not query Wolverine message storage.", ex);
        }
    }
}
