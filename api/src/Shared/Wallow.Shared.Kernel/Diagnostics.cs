using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Wallow.Shared.Kernel;

public static class Diagnostics
{
    private static string _prefix = "Wallow";

    public static Meter Meter { get; private set; } = new("Wallow");
    public static ActivitySource ActivitySource { get; private set; } = new("Wallow");

    private static Meter _messagingMeter = CreateMeter("Messaging");

    public static Counter<long> MessagesTotal { get; private set; } =
        _messagingMeter.CreateCounter<long>(
            "wallow.messaging.messages_total",
            description: "Total number of messages processed");

    public static Histogram<double> MessageDuration { get; private set; } =
        _messagingMeter.CreateHistogram<double>(
            "wallow.messaging.message_duration",
            unit: "ms",
            description: "Duration of message processing in milliseconds");

    public static Counter<long> DomainEventsPublishedTotal { get; private set; } =
        _messagingMeter.CreateCounter<long>(
            "wallow.messaging.domain_events_published_total",
            description: "Total number of domain events published");

    /// <summary>
    /// Initializes telemetry sources with a custom prefix. Must be called early in startup
    /// before any telemetry is emitted. Defaults to "Wallow" if never called.
    /// </summary>
    public static void Initialize(string prefix)
    {
        _prefix = prefix;
        Meter = new(prefix);
        ActivitySource = new(prefix);
        _messagingMeter = CreateMeter("Messaging");

        MessagesTotal = _messagingMeter.CreateCounter<long>(
            $"{prefix.ToLowerInvariant()}.messaging.messages_total",
            description: "Total number of messages processed");

        MessageDuration = _messagingMeter.CreateHistogram<double>(
            $"{prefix.ToLowerInvariant()}.messaging.message_duration",
            unit: "ms",
            description: "Duration of message processing in milliseconds");

        DomainEventsPublishedTotal = _messagingMeter.CreateCounter<long>(
            $"{prefix.ToLowerInvariant()}.messaging.domain_events_published_total",
            description: "Total number of domain events published");
    }

    public static ActivitySource CreateActivitySource(string moduleName) =>
        new($"{_prefix}.{moduleName}");

    public static Meter CreateMeter(string moduleName) =>
        new($"{_prefix}.{moduleName}");
}
