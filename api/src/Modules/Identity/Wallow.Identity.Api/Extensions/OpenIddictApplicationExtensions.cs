using System.Text.Json;
using OpenIddict.Abstractions;
using Wallow.Identity.Application.Helpers;

namespace Wallow.Identity.Api.Extensions;

/// <summary>
/// Reads and writes Wallow-defined properties on a client record. The Infrastructure layer carries
/// a twin of this class for its own callers, because the Api layer may not depend on it; both
/// address each property through its <see cref="ClientApplicationProperties"/> key, which is what
/// keeps the twins one key per property.
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
