using System.Diagnostics;
using Foundry.Shared.Kernel.MultiTenancy;
using OpenTelemetry;

namespace Foundry.Api.Middleware;

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

            Baggage.SetBaggage("foundry.tenant_id", tenantId);

            if (Activity.Current is { } activity)
            {
                activity.SetTag("foundry.tenant_id", tenantId);
            }
        }

        await _next(context);
    }
}
