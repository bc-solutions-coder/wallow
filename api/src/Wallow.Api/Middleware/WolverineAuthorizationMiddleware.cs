using Wolverine;

namespace Wallow.Api.Middleware;

public static class WolverineAuthorizationMiddleware
{
    private const string TenantIdHeader = "X-Tenant-Id";

    public static void Before(Envelope envelope)
    {
        // Local or unspecified destinations do not require this header.
        if (envelope.Destination?.Scheme is null or "local")
        {
            return;
        }

        // Remote destinations require a nonblank tenant header.
        if (!envelope.Headers.TryGetValue(TenantIdHeader, out string? tenantId)
            || string.IsNullOrWhiteSpace(tenantId))
        {
            throw new UnauthorizedAccessException(
                "External message is missing required tenant context (X-Tenant-Id header).");
        }
    }
}
