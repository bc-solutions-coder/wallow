namespace Wallow.Shared.Kernel.Tests.Diagnostics;

public class DiagnosticsTests
{
    [Fact]
    public void Meter_IsNotNull_AndHasExpectedName()
    {
        Kernel.Diagnostics.Meter.Should().NotBeNull();
        Kernel.Diagnostics.Meter.Name.Should().Be("Wallow");
    }

    [Fact]
    public void ActivitySource_IsNotNull_AndHasExpectedName()
    {
        Kernel.Diagnostics.ActivitySource.Should().NotBeNull();
        Kernel.Diagnostics.ActivitySource.Name.Should().Be("Wallow");
    }

    [Fact]
    public void CreateMeter_WithModuleName_ReturnsMeterWithPrefixedName()
    {
        using System.Diagnostics.Metrics.Meter meter = Kernel.Diagnostics.CreateMeter("Billing");

        meter.Should().NotBeNull();
        meter.Name.Should().Be("Wallow.Billing");
    }

    [Fact]
    public void CreateActivitySource_WithModuleName_ReturnsActivitySourceWithPrefixedName()
    {
        using System.Diagnostics.ActivitySource source = Kernel.Diagnostics.CreateActivitySource("Billing");

        source.Should().NotBeNull();
        source.Name.Should().Be("Wallow.Billing");
    }
}
