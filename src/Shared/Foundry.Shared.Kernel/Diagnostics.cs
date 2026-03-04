using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Foundry.Shared.Kernel;

public static class Diagnostics
{
    public static readonly Meter Meter = new("Foundry");
    public static readonly ActivitySource ActivitySource = new("Foundry");

    private static readonly Meter _messagingMeter = CreateMeter("Messaging");

    public static readonly Counter<long> MessagesTotal =
        _messagingMeter.CreateCounter<long>(
            "foundry.messaging.messages_total",
            description: "Total number of messages processed");

    public static readonly Histogram<double> MessageDuration =
        _messagingMeter.CreateHistogram<double>(
            "foundry.messaging.message_duration",
            unit: "ms",
            description: "Duration of message processing in milliseconds");

    public static readonly Counter<long> DomainEventsPublishedTotal =
        _messagingMeter.CreateCounter<long>(
            "foundry.messaging.domain_events_published_total",
            description: "Total number of domain events published");

    public static ActivitySource CreateActivitySource(string moduleName) =>
        new($"Foundry.{moduleName}");

    public static Meter CreateMeter(string moduleName) =>
        new($"Foundry.{moduleName}");
}
