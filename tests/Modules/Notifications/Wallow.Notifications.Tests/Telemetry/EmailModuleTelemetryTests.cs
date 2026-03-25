using System.Diagnostics;
using Wallow.Notifications.Application.Channels.Email.Telemetry;

namespace Wallow.Notifications.Tests.Telemetry;

public sealed class EmailModuleTelemetryTests : IDisposable
{
    private readonly ActivityListener _listener;

    public EmailModuleTelemetryTests()
    {
        _listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name.Contains("Notifications"),
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded
        };
        ActivitySource.AddActivityListener(_listener);
    }

    public void Dispose()
    {
        _listener.Dispose();
    }

    [Fact]
    public void ActivitySource_IsNotNull()
    {
        EmailModuleTelemetry.ActivitySource.Should().NotBeNull();
    }

    [Fact]
    public void ActivitySource_HasCorrectName()
    {
        EmailModuleTelemetry.ActivitySource.Name.Should().Contain("Notifications.Email");
    }

    [Fact]
    public void EmailSentTotal_IsNotNull()
    {
        EmailModuleTelemetry.EmailSentTotal.Should().NotBeNull();
    }

    [Fact]
    public void EmailSentTotal_HasCorrectName()
    {
        EmailModuleTelemetry.EmailSentTotal.Name.Should().Be("wallow.notifications.email_sent_total");
    }

    [Fact]
    public void EmailFailedTotal_IsNotNull()
    {
        EmailModuleTelemetry.EmailFailedTotal.Should().NotBeNull();
    }

    [Fact]
    public void EmailFailedTotal_HasCorrectName()
    {
        EmailModuleTelemetry.EmailFailedTotal.Name.Should().Be("wallow.notifications.email_failed_total");
    }

    [Fact]
    public void EmailSendDuration_IsNotNull()
    {
        EmailModuleTelemetry.EmailSendDuration.Should().NotBeNull();
    }

    [Fact]
    public void EmailSendDuration_HasCorrectName()
    {
        EmailModuleTelemetry.EmailSendDuration.Name.Should().Be("wallow.notifications.email_send_duration");
    }

    [Fact]
    public void StartSendEmailActivity_ReturnsActivity()
    {
        using Activity? activity = EmailModuleTelemetry.StartSendEmailActivity(3);

        activity.Should().NotBeNull();
    }

    [Fact]
    public void StartSendEmailActivity_SetsModuleTag()
    {
        using Activity? activity = EmailModuleTelemetry.StartSendEmailActivity(3);

        activity!.GetTagItem("wallow.module").Should().Be("Notifications");
    }

    [Fact]
    public void StartSendEmailActivity_SetsRecipientCountTag()
    {
        using Activity? activity = EmailModuleTelemetry.StartSendEmailActivity(3);

        activity!.GetTagItem("recipient_count").Should().Be(3);
    }

    [Fact]
    public void StartSendEmailActivity_WithTemplateId_SetsTemplateIdTag()
    {
        using Activity? activity = EmailModuleTelemetry.StartSendEmailActivity(1, "welcome-email");

        activity!.GetTagItem("template_id").Should().Be("welcome-email");
    }

    [Fact]
    public void StartSendEmailActivity_WithoutTemplateId_DoesNotSetTemplateIdTag()
    {
        using Activity? activity = EmailModuleTelemetry.StartSendEmailActivity(1);

        activity!.GetTagItem("template_id").Should().BeNull();
    }

    [Fact]
    public void StartSendNotificationActivity_ReturnsActivity()
    {
        using Activity? activity = EmailModuleTelemetry.StartSendNotificationActivity(5);

        activity.Should().NotBeNull();
    }

    [Fact]
    public void StartSendNotificationActivity_SetsModuleTag()
    {
        using Activity? activity = EmailModuleTelemetry.StartSendNotificationActivity(5);

        activity!.GetTagItem("wallow.module").Should().Be("Notifications");
    }

    [Fact]
    public void StartSendNotificationActivity_SetsRecipientCountTag()
    {
        using Activity? activity = EmailModuleTelemetry.StartSendNotificationActivity(5);

        activity!.GetTagItem("recipient_count").Should().Be(5);
    }

    [Fact]
    public void StartSendNotificationActivity_WithTemplateId_SetsTemplateIdTag()
    {
        using Activity? activity = EmailModuleTelemetry.StartSendNotificationActivity(2, "inquiry-comment");

        activity!.GetTagItem("template_id").Should().Be("inquiry-comment");
    }

    [Fact]
    public void StartSendNotificationActivity_WithoutTemplateId_DoesNotSetTemplateIdTag()
    {
        using Activity? activity = EmailModuleTelemetry.StartSendNotificationActivity(2);

        activity!.GetTagItem("template_id").Should().BeNull();
    }
}
