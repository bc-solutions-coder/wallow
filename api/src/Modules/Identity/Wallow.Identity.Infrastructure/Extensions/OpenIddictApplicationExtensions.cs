using System.Text.Json;
using OpenIddict.Abstractions;
using Wallow.Identity.Application.Helpers;

namespace Wallow.Identity.Infrastructure.Extensions;

/// <summary>
/// Reads and writes the tenant on a client record. The Api layer carries a twin of this class for
/// its own callers, because it may not depend on this project; both address the property through
/// <see cref="ClientApplicationProperties.TenantId"/>, which is what keeps them one key.
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

    /// <summary>Writes the front-channel logout URI; <see langword="null"/> removes it.</summary>
    public static void SetFrontchannelLogoutUri(this OpenIddictApplicationDescriptor descriptor, Uri? uri)
    {
        if (uri is null)
        {
            descriptor.Properties.Remove(ClientApplicationProperties.FrontchannelLogoutUri);
            return;
        }

        descriptor.Properties[ClientApplicationProperties.FrontchannelLogoutUri] =
            JsonSerializer.SerializeToElement(uri.AbsoluteUri);
    }

    public static Uri? GetFrontchannelLogoutUri(this OpenIddictApplicationDescriptor descriptor)
    {
        if (descriptor.Properties.TryGetValue(
                ClientApplicationProperties.FrontchannelLogoutUri, out JsonElement element)
            && element.GetString() is string value
            && Uri.TryCreate(value, UriKind.Absolute, out Uri? uri))
        {
            return uri;
        }

        return null;
    }
}
