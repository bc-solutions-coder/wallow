using System.Text.Json;
using OpenIddict.Abstractions;
using Wallow.Identity.Application.Helpers;

namespace Wallow.Identity.Api.Extensions;

/// <summary>
/// Reads and writes the tenant on a client record. The Infrastructure layer carries a twin of this
/// class for its own callers, because the Api layer may not depend on it; both address the property
/// through <see cref="ClientApplicationProperties.TenantId"/>, which is what keeps them one key.
/// </summary>
public static class OpenIddictApplicationExtensions
{
    public static void SetTenantId(this OpenIddictApplicationDescriptor descriptor, string tenantId)
    {
        descriptor.Properties[ClientApplicationProperties.TenantId] =
            JsonSerializer.SerializeToElement(tenantId);
    }

    public static string? GetTenantId(this OpenIddictApplicationDescriptor descriptor)
    {
        if (descriptor.Properties.TryGetValue(ClientApplicationProperties.TenantId, out JsonElement element))
        {
            return element.GetString();
        }

        return null;
    }
}
