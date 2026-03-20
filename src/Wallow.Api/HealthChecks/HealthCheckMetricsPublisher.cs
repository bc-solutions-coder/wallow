using System.Diagnostics.Metrics;
using Wallow.Shared.Kernel;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Wallow.Api.HealthChecks;

internal sealed class HealthCheckMetricsPublisher : IHealthCheckPublisher
{
    private static readonly Meter _healthMeter = Diagnostics.CreateMeter("Health");

    private static readonly Gauge<int> _healthCheckStatus = _healthMeter.CreateGauge<int>(
        "wallow.healthcheck.status",
        description: "Health check status (0=unhealthy, 1=healthy)");

    public Task PublishAsync(HealthReport report, CancellationToken cancellationToken)
    {
        foreach (KeyValuePair<string, HealthReportEntry> entry in report.Entries)
        {
            int statusValue = entry.Value.Status == HealthStatus.Healthy ? 1 : 0;
            _healthCheckStatus.Record(statusValue, new KeyValuePair<string, object?>("check_name", entry.Key));
        }

        return Task.CompletedTask;
    }
}
