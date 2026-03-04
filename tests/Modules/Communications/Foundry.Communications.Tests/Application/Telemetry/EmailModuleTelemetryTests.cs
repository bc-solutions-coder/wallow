using Foundry.Communications.Application.Channels.Email.Telemetry;

namespace Foundry.Communications.Tests.Application.Telemetry;

public class EmailModuleTelemetryTests
{
    [Fact]
    public void ActivitySource_HasCorrectName()
    {
        EmailModuleTelemetry.ActivitySource.Name.Should().Be("Foundry.Communications.Email");
    }

    [Fact]
    public void ActivitySource_IsNotNull()
    {
        EmailModuleTelemetry.ActivitySource.Should().NotBeNull();
    }

    [Fact]
    public void ActivitySource_IsSameInstanceOnMultipleAccess()
    {
        System.Diagnostics.ActivitySource first = EmailModuleTelemetry.ActivitySource;
        System.Diagnostics.ActivitySource second = EmailModuleTelemetry.ActivitySource;

        first.Should().BeSameAs(second);
    }

    [Fact]
    public void EmailSentTotal_IsNotNull()
    {
        EmailModuleTelemetry.EmailSentTotal.Should().NotBeNull();
    }

    [Fact]
    public void EmailFailedTotal_IsNotNull()
    {
        EmailModuleTelemetry.EmailFailedTotal.Should().NotBeNull();
    }

    [Fact]
    public void EmailSendDuration_IsNotNull()
    {
        EmailModuleTelemetry.EmailSendDuration.Should().NotBeNull();
    }
}
