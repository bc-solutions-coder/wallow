using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Wallow.Shared.Contracts.Inquiries.Events;
using Wallow.Tests.Common.Factories;
using Wolverine.Persistence.Durability;
using Wolverine.Tracking;

namespace Wallow.Api.Tests.Integration;

/// <summary>
/// Checks that a poisoned email event reaches persistent dead-letter storage and degrades the DLQ health entry.
/// The readiness response must omit that entry.
/// </summary>
[Collection(nameof(ApiIntegrationTestCollection))]
[Trait("Category", "Integration")]
public sealed class DeadLetterObservabilityTests(WallowApiFactory factory)
{
    /// <summary>
    /// Invalid recipient used to trigger email validation failure.
    /// </summary>
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

        // Read the detailed health body even when another check makes the HTTP status unsuccessful.
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
    /// Polls for a nonzero dead-letter count for up to 15 seconds.
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
