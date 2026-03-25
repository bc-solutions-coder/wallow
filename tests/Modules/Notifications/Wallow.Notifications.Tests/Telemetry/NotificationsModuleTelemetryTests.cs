using Wallow.Notifications.Application.Channels.InApp.Telemetry;

namespace Wallow.Notifications.Tests.Telemetry;

public class NotificationsModuleTelemetryTests
{
    [Fact]
    public void ActivitySource_IsNotNull()
    {
        NotificationsModuleTelemetry.ActivitySource.Should().NotBeNull();
    }

    [Fact]
    public void ActivitySource_HasCorrectName()
    {
        NotificationsModuleTelemetry.ActivitySource.Name.Should().Contain("Notifications.InApp");
    }

    [Fact]
    public void NotificationSentTotal_IsNotNull()
    {
        NotificationsModuleTelemetry.NotificationSentTotal.Should().NotBeNull();
    }

    [Fact]
    public void NotificationSentTotal_HasCorrectName()
    {
        NotificationsModuleTelemetry.NotificationSentTotal.Name.Should().Be("wallow.notifications.notification_sent_total");
    }

    [Fact]
    public void NotificationFailedTotal_IsNotNull()
    {
        NotificationsModuleTelemetry.NotificationFailedTotal.Should().NotBeNull();
    }

    [Fact]
    public void NotificationFailedTotal_HasCorrectName()
    {
        NotificationsModuleTelemetry.NotificationFailedTotal.Name.Should().Be("wallow.notifications.notification_failed_total");
    }
}
