using System.Diagnostics;
using Wallow.Shared.Kernel.MultiTenancy;
using OpenTelemetry;

namespace Wallow.Api.Middleware;

internal sealed class TenantBaggageMiddleware
{
    private readonly RequestDelegate _next;

    public TenantBaggageMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ITenantContext tenantContext)
    {
        if (tenantContext.IsResolved)
        {
            string tenantId = tenantContext.TenantId.Value.ToString();

            Baggage.SetBaggage("wallow.tenant_id", tenantId);

            if (Activity.Current is { } activity)
            {
                activity.SetTag("wallow.tenant_id", tenantId);
            }
        }

        await _next(context);
    }
}
