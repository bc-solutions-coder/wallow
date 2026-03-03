using System.Diagnostics;
using System.Diagnostics.Metrics;
using Foundry.Shared.Kernel;

namespace Foundry.Identity.Application.Telemetry;

public static class IdentityModuleTelemetry
{
    public static readonly ActivitySource ActivitySource = Diagnostics.CreateActivitySource("Identity");
    private static readonly Meter _meter = Diagnostics.CreateMeter("Identity");

    public static readonly Counter<long> SsoLoginsTotal =
        _meter.CreateCounter<long>("foundry.identity.sso_logins_total");

    public static readonly Counter<long> SsoFailuresTotal =
        _meter.CreateCounter<long>("foundry.identity.sso_failures_total");
}
