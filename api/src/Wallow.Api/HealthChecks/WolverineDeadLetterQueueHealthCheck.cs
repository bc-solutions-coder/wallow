using System.Diagnostics.Metrics;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Wallow.Shared.Kernel;
using Wolverine.Logging;
using Wolverine.Persistence.Durability;

namespace Wallow.Api.HealthChecks;

/// <summary>
/// Surfaces the Wolverine dead-letter queue as a health signal and a depth gauge (Wallow-qi90.2).
/// A dead-lettered envelope is work the API accepted, answered 200 for, and then silently dropped
/// after retry exhaustion; before this check the only evidence was a row in
/// <c>wolverine.wolverine_dead_letters</c> that nothing read. A non-empty queue reports
/// <see cref="HealthStatus.Degraded" /> rather than Unhealthy, and the registration deliberately
/// omits the "ready" tag: a poison message must show up on <c>/health</c> without failing
/// readiness probes and restart-looping the container. The depth lands on the
/// <c>Wallow.Messaging</c> meter every evaluation, so the periodic health publisher doubles as
/// the metric sampler.
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
