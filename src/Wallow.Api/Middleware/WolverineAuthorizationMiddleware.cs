using Wolverine;

namespace Wallow.Api.Middleware;

public static class WolverineAuthorizationMiddleware
{
    private const string TenantIdHeader = "X-Tenant-Id";

    public static void Before(Envelope envelope)
    {
        // Skip authorization for in-process (local) messages
        if (envelope.Destination?.Scheme is null or "local")
        {
            return;
        }

        // External messages (e.g. RabbitMQ) must carry a tenant ID
        if (!envelope.Headers.TryGetValue(TenantIdHeader, out string? tenantId)
            || string.IsNullOrWhiteSpace(tenantId))
        {
            throw new UnauthorizedAccessException(
                "External message is missing required tenant context (X-Tenant-Id header).");
        }
    }
}
