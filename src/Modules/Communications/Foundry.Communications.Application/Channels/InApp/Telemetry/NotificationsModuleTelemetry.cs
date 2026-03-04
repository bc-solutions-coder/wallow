using System.Diagnostics;
using System.Diagnostics.Metrics;
using Foundry.Shared.Kernel;

namespace Foundry.Communications.Application.Channels.InApp.Telemetry;

public static class NotificationsModuleTelemetry
{
    public static readonly ActivitySource ActivitySource = Diagnostics.CreateActivitySource("Communications.InApp");
    private static readonly Meter _meter = Diagnostics.CreateMeter("Communications");

    public static readonly Counter<long> NotificationSentTotal =
        _meter.CreateCounter<long>("foundry.communications.notification_sent_total");

    public static readonly Counter<long> NotificationFailedTotal =
        _meter.CreateCounter<long>("foundry.communications.notification_failed_total");
}
