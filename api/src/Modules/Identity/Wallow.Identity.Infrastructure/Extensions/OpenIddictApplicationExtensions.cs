using System.Text.Json;
using OpenIddict.Abstractions;

namespace Wallow.Identity.Infrastructure.Extensions;

public static class OpenIddictApplicationExtensions
{
    private const string TenantIdPropertyKey = "tenant_id";

    public static void SetTenantId(this OpenIddictApplicationDescriptor descriptor, string tenantId)
    {
        descriptor.Properties[TenantIdPropertyKey] = JsonSerializer.SerializeToElement(tenantId);
    }

    public static string? GetTenantId(this OpenIddictApplicationDescriptor descriptor)
    {
        if (descriptor.Properties.TryGetValue(TenantIdPropertyKey, out JsonElement element))
        {
            return element.GetString();
        }

        return null;
    }
}
