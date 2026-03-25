using System.Diagnostics;
using Wallow.Notifications.Application.Channels.Email.Telemetry;

namespace Wallow.Notifications.Tests.Telemetry;

public class EmailModuleTelemetryTests
{
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
    public void StartSendEmailActivity_ReturnsActivityOrNull()
    {
        Activity? activity = EmailModuleTelemetry.StartSendEmailActivity(3);
        activity?.Dispose();
    }

    [Fact]
    public void StartSendEmailActivity_WithTemplateId_ReturnsActivityOrNull()
    {
        Activity? activity = EmailModuleTelemetry.StartSendEmailActivity(1, "welcome-email");
        activity?.Dispose();
    }

    [Fact]
    public void StartSendNotificationActivity_ReturnsActivityOrNull()
    {
        Activity? activity = EmailModuleTelemetry.StartSendNotificationActivity(5);
        activity?.Dispose();
    }

    [Fact]
    public void StartSendNotificationActivity_WithTemplateId_ReturnsActivityOrNull()
    {
        Activity? activity = EmailModuleTelemetry.StartSendNotificationActivity(2, "inquiry-comment");
        activity?.Dispose();
    }
}
