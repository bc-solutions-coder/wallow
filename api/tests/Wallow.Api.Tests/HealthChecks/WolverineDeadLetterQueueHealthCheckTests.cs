using System.Diagnostics.Metrics;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using NSubstitute.ExceptionExtensions;
using Wallow.Api.HealthChecks;
using Wolverine.Logging;
using Wolverine.Persistence.Durability;

namespace Wallow.Api.Tests.HealthChecks;

public class WolverineDeadLetterQueueHealthCheckTests
{
    private static IMessageStore StoreWithDeadLetterCount(int deadLetterCount)
    {
        // NSubstitute auto-substitutes the Admin property, so only the count needs configuring.
        IMessageStore store = Substitute.For<IMessageStore>();
        store.Admin.FetchCountsAsync().Returns(new PersistedCounts { DeadLetter = deadLetterCount });
        return store;
    }

    [Fact]
    public async Task CheckHealthAsync_WithAnEmptyDeadLetterQueue_ReturnsHealthy()
    {
        WolverineDeadLetterQueueHealthCheck sut = new(StoreWithDeadLetterCount(0));

        HealthCheckResult result = await sut.CheckHealthAsync(new HealthCheckContext());

        result.Status.Should().Be(HealthStatus.Healthy);
    }

    [Fact]
    public async Task CheckHealthAsync_WithDeadLetteredEnvelopes_ReturnsDegraded_NamingTheCount()
    {
        WolverineDeadLetterQueueHealthCheck sut = new(StoreWithDeadLetterCount(3));

        HealthCheckResult result = await sut.CheckHealthAsync(new HealthCheckContext());

        result.Status.Should().Be(
            HealthStatus.Degraded,
            "a poison message is degraded service, not a dead process — Unhealthy would let a " +
            "single bad envelope fail orchestrator probes and restart-loop the container");
        result.Description.Should().Contain("3");
    }

    [Fact]
    public async Task CheckHealthAsync_WhenStorageIsUnreachable_ReturnsUnhealthy_WithTheException()
    {
        IMessageStore store = Substitute.For<IMessageStore>();
        InvalidOperationException failure = new("storage down");
        store.Admin.FetchCountsAsync().ThrowsAsync(failure);
        WolverineDeadLetterQueueHealthCheck sut = new(store);

        HealthCheckResult result = await sut.CheckHealthAsync(new HealthCheckContext());

        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Exception.Should().BeSameAs(failure);
    }

    [Fact]
    public async Task CheckHealthAsync_RecordsTheDepthGauge()
    {
        long? recordedDepth = null;
        using MeterListener listener = new();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Name == "wallow.messaging.dead_letter_queue.depth")
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>(
            (_, measurement, _, _) => recordedDepth = measurement);
        listener.Start();

        WolverineDeadLetterQueueHealthCheck sut = new(StoreWithDeadLetterCount(7));
        await sut.CheckHealthAsync(new HealthCheckContext());

        recordedDepth.Should().Be(
            7,
            "the health check doubles as the depth sampler: the periodic health publisher drives " +
            "it, so every evaluation must land the current queue depth on the messaging meter");
    }
}
