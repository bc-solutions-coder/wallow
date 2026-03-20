using System.Diagnostics;
using System.Diagnostics.Metrics;
using Wallow.Shared.Kernel;

namespace Wallow.Notifications.Application.Channels.InApp.Telemetry;

public static class NotificationsModuleTelemetry
{
    public static readonly ActivitySource ActivitySource = Diagnostics.CreateActivitySource("Notifications.InApp");
    private static readonly Meter _meter = Diagnostics.CreateMeter("Notifications");

    public static readonly Counter<long> NotificationSentTotal =
        _meter.CreateCounter<long>("wallow.notifications.notification_sent_total");

    public static readonly Counter<long> NotificationFailedTotal =
        _meter.CreateCounter<long>("wallow.notifications.notification_failed_total");
}
