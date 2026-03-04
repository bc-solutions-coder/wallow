using System.Diagnostics;
using System.Diagnostics.Metrics;
using Foundry.Shared.Kernel;

namespace Foundry.Configuration.Application.Telemetry;

public static class ConfigurationModuleTelemetry
{
    public static readonly ActivitySource ActivitySource = Diagnostics.CreateActivitySource("Configuration");
    private static readonly Meter _meter = Diagnostics.CreateMeter("Configuration");

    public static readonly Counter<long> ReadsTotal =
        _meter.CreateCounter<long>("foundry.configuration.reads_total");
}
