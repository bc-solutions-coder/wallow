using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Wallow.Shared.Contracts.Inquiries.Events;
using Wallow.Tests.Common.Factories;
using Wolverine.Persistence.Durability;
using Wolverine.Tracking;

namespace Wallow.Api.Tests.Integration;

/// <summary>
/// Pins the dead-letter observability chain end to end (Wallow-qi90.2): a handler that exhausts
/// its retries must terminate in the tracked <c>MovedToErrorQueue</c> event, leave a persisted
/// row Wolverine's storage counts can see, and degrade the <c>wolverine-dlq</c> entry on
/// <c>/health</c> — while never touching <c>/health/ready</c>, because a poison message must not
/// fail readiness and restart-loop the container.
/// <para>
/// The poison is data, not a test double: <c>SendEmailValidator</c> rejects a recipient that is
/// not an email address, so an <see cref="InquiryStatusChangedEvent" /> carrying one makes
/// exactly the email handler fail through the standard retry policy into the DLQ (the same
/// lever <c>MultipleHandlerSeparationTests</c> uses).
/// </para>
/// <para>
/// The tracked <c>MovedToErrorQueue</c> record is the pin on the Error log the bead asks for:
/// the same <c>WolverineRuntime</c> method raises the tracking event, increments the
/// <c>wolverine-dead-letter-queue</c> counter, and writes "Envelope … was moved to the error
/// queue" at Error with the exception attached. <c>WolverineDeadLetterLoggingTests</c> guards
/// the Serilog side, so together they pin log emission without capturing sinks.
/// </para>
/// </summary>
[Collection(nameof(ApiIntegrationTestCollection))]
[Trait("Category", "Integration")]
public sealed class DeadLetterObservabilityTests(WallowApiFactory factory)
{
    /// <summary>Not an email address, so the email handler dead-letters on validation.</summary>
    private const string PoisonedRecipient = "dlq-observability-probe-not-an-email";

    private static readonly TimeSpan _trackingTimeout = TimeSpan.FromSeconds(60);

    [Fact]
    public async Task RetryExhaustion_LandsInTheDlq_AndDegradesTheHealthEndpoint()
    {
        ITrackedSession session = await factory.Services.TrackActivity()
            .DoNotAssertOnExceptionsDetected()
            .Timeout(_trackingTimeout)
            .PublishMessageAndWaitAsync(new InquiryStatusChangedEvent
            {
                InquiryId = Guid.NewGuid(),
                OldStatus = "New",
                NewStatus = "Reviewed",
                ChangedAt = DateTime.UtcNow,
                SubmitterEmail = PoisonedRecipient
            }, null);

        session.MovedToErrorQueue.RecordsInOrder()
            .Where(record => record.Message is InquiryStatusChangedEvent)
            .Should().NotBeEmpty(
                "retry exhaustion must terminate in the MovedToErrorQueue runtime event — the " +
                "one that increments the dead-letter counter and writes the Error log");

        IMessageStore messageStore = factory.Services.GetRequiredService<IMessageStore>();
        int depth = await WaitForDeadLetterDepthAsync(messageStore);

        depth.Should().BeGreaterThan(
            0,
            "the dead-lettered envelope must be persisted where FetchCountsAsync can count it, " +
            "or the health check has nothing to observe");

        using HttpClient client = factory.CreateClient();

        // GetAsync, not GetStringAsync: /health answers 503 whenever ANY check is unhealthy
        // (HealthCheckTests accepts both), and the detailed body is what this test is after.
        using HttpResponseMessage health = await client.GetAsync("/health");
        string healthBody = await health.Content.ReadAsStringAsync();
        using JsonDocument healthDocument = JsonDocument.Parse(healthBody);
        JsonElement dlqEntry = healthDocument.RootElement.GetProperty("checks").EnumerateArray()
            .Single(check => check.GetProperty("name").GetString() == "wolverine-dlq");

        dlqEntry.GetProperty("status").GetString().Should().Be(
            "Degraded",
            "a non-empty dead-letter queue is degraded service: visible on /health, but never " +
            "a dead process");

        using HttpResponseMessage ready = await client.GetAsync("/health/ready");
        string readyBody = await ready.Content.ReadAsStringAsync();
        using JsonDocument readyDocument = JsonDocument.Parse(readyBody);

        readyDocument.RootElement.GetProperty("checks").EnumerateArray()
            .Select(check => check.GetProperty("name").GetString())
            .Should().NotContain(
                "wolverine-dlq",
                "the check must not carry the \"ready\" tag — a poison message failing " +
                "readiness would restart-loop the container without fixing anything");
    }

    /// <summary>
    /// The storage write happens on the dead-letter path itself, but poll briefly anyway so a
    /// slow container round-trip cannot flake this: the claim under test is "persisted", not
    /// "persisted within one scheduler tick".
    /// </summary>
    private static async Task<int> WaitForDeadLetterDepthAsync(IMessageStore messageStore)
    {
        DateTime deadline = DateTime.UtcNow + TimeSpan.FromSeconds(15);
        int depth = (await messageStore.Admin.FetchCountsAsync()).DeadLetter;

        while (depth == 0 && DateTime.UtcNow < deadline)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(250));
            depth = (await messageStore.Admin.FetchCountsAsync()).DeadLetter;
        }

        return depth;
    }
}
