using System.Diagnostics;
using System.Diagnostics.Metrics;
using Wallow.Shared.Kernel;

namespace Wallow.Identity.Application.Telemetry;

public static class IdentityModuleTelemetry
{
    public static readonly ActivitySource ActivitySource = Diagnostics.CreateActivitySource("Identity");
    private static readonly Meter _meter = Diagnostics.CreateMeter("Identity");

    public static readonly Counter<long> SsoLoginsTotal =
        _meter.CreateCounter<long>("wallow.identity.sso_logins_total");

    public static readonly Counter<long> SsoFailuresTotal =
        _meter.CreateCounter<long>("wallow.identity.sso_failures_total");

    public static readonly Counter<long> RequestsAuthenticatedTotal =
        _meter.CreateCounter<long>("wallow.requests_authenticated_total",
            description: "Total authenticated requests");
}
