using System.Diagnostics;
using System.Diagnostics.Metrics;
using Foundry.Shared.Kernel;

namespace Foundry.Communications.Application.Channels.Email.Telemetry;

public static class EmailModuleTelemetry
{
    public static readonly ActivitySource ActivitySource = Diagnostics.CreateActivitySource("Communications.Email");
    private static readonly Meter _meter = Diagnostics.CreateMeter("Communications");

    public static readonly Counter<long> EmailSentTotal =
        _meter.CreateCounter<long>("foundry.communications.email_sent_total");

    public static readonly Counter<long> EmailFailedTotal =
        _meter.CreateCounter<long>("foundry.communications.email_failed_total");

    public static readonly Histogram<double> EmailSendDuration =
        _meter.CreateHistogram<double>("foundry.communications.email_send_duration", "ms");

    public static Activity? StartSendEmailActivity(int recipientCount, string? templateId = null)
    {
        Activity? activity = ActivitySource.StartActivity("Communications.SendEmail");
        if (activity is not null)
        {
            activity.SetTag("foundry.module", "Communications");
            activity.SetTag("recipient_count", recipientCount);
            if (templateId is not null)
            {
                activity.SetTag("template_id", templateId);
            }
        }
        return activity;
    }

    public static Activity? StartSendNotificationActivity(int recipientCount, string? templateId = null)
    {
        Activity? activity = ActivitySource.StartActivity("Communications.SendNotification");
        if (activity is not null)
        {
            activity.SetTag("foundry.module", "Communications");
            activity.SetTag("recipient_count", recipientCount);
            if (templateId is not null)
            {
                activity.SetTag("template_id", templateId);
            }
        }
        return activity;
    }
}
