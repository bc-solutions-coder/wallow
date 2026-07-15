using Microsoft.Extensions.Diagnostics.HealthChecks;
using Wallow.Api.HealthChecks;

namespace Wallow.Api.Tests.HealthChecks;

public class HealthCheckMetricsPublisherTests
{
    private readonly HealthCheckMetricsPublisher _sut = new();

    [Fact]
    public async Task PublishAsync_WithHealthyEntries_CompletesSuccessfully()
    {
        HealthReport report = CreateReport(HealthStatus.Healthy, "test-check");

        Task publishTask = _sut.PublishAsync(report, CancellationToken.None);

        await publishTask;
        publishTask.IsCompletedSuccessfully.Should().BeTrue();
    }

    [Fact]
    public async Task PublishAsync_WithUnhealthyEntries_CompletesSuccessfully()
    {
        HealthReport report = CreateReport(HealthStatus.Unhealthy, "test-check");

        Task publishTask = _sut.PublishAsync(report, CancellationToken.None);

        await publishTask;
        publishTask.IsCompletedSuccessfully.Should().BeTrue();
    }

    [Fact]
    public async Task PublishAsync_WithMultipleEntries_ProcessesAll()
    {
        Dictionary<string, HealthReportEntry> entries = new()
        {
            ["db"] = new HealthReportEntry(HealthStatus.Healthy, null, TimeSpan.Zero, null, null),
            ["redis"] = new HealthReportEntry(HealthStatus.Unhealthy, null, TimeSpan.Zero, null, null),
            ["s3"] = new HealthReportEntry(HealthStatus.Degraded, null, TimeSpan.Zero, null, null),
        };
        HealthReport report = new(entries, TimeSpan.FromMilliseconds(100));

        Task publishTask = _sut.PublishAsync(report, CancellationToken.None);

        await publishTask;
        publishTask.IsCompletedSuccessfully.Should().BeTrue();
    }

    [Fact]
    public async Task PublishAsync_WithEmptyEntries_CompletesSuccessfully()
    {
        Dictionary<string, HealthReportEntry> entries = new();
        HealthReport report = new(entries, TimeSpan.Zero);

        Task publishTask = _sut.PublishAsync(report, CancellationToken.None);

        await publishTask;
        publishTask.IsCompletedSuccessfully.Should().BeTrue();
    }

    private static HealthReport CreateReport(HealthStatus status, string checkName)
    {
        Dictionary<string, HealthReportEntry> entries = new()
        {
            [checkName] = new HealthReportEntry(status, null, TimeSpan.Zero, null, null)
        };
        return new HealthReport(entries, TimeSpan.FromMilliseconds(50));
    }
}
