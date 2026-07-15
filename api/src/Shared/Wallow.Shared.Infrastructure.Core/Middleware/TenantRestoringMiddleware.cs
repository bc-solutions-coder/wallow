using System.Collections.Concurrent;
using System.Reflection;
using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.MultiTenancy;
using Wolverine;

namespace Wallow.Shared.Infrastructure.Core.Middleware;

public static class TenantRestoringMiddleware
{
    private static readonly ConcurrentDictionary<Type, PropertyInfo?> _tenantIdPropertyCache = new();

    public static void Before(Envelope envelope, ITenantContextSetter tenantContextSetter)
    {
        // Primary: read from envelope header (stamped by TenantStampingMiddleware)
        if (envelope.Headers.TryGetValue("X-Tenant-Id", out string? tenantHeader)
            && Guid.TryParse(tenantHeader, out Guid tenantGuid))
        {
            tenantContextSetter.SetTenant(TenantId.Create(tenantGuid));
            return;
        }

        // Fallback: read TenantId from message body (for integration events published
        // via bus.PublishAsync where the outgoing envelope doesn't carry the header)
        if (envelope.Message is not null && TryGetTenantIdFromMessage(envelope.Message, out Guid messageTenantId))
        {
            tenantContextSetter.SetTenant(TenantId.Create(messageTenantId));
        }
    }

    private static bool TryGetTenantIdFromMessage(object message, out Guid tenantId)
    {
        tenantId = Guid.Empty;

        PropertyInfo? property = _tenantIdPropertyCache.GetOrAdd(
            message.GetType(),
            static type => type.GetProperty("TenantId", BindingFlags.Public | BindingFlags.Instance));

        if (property is null || property.PropertyType != typeof(Guid))
        {
            return false;
        }

        tenantId = (Guid)property.GetValue(message)!;
        return tenantId != Guid.Empty;
    }
}
