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

    /// <summary>Writes the back-channel logout URI; <see langword="null"/> removes it.</summary>
    public static void SetBackchannelLogoutUri(this OpenIddictApplicationDescriptor descriptor, Uri? uri)
    {
        if (uri is null)
        {
            descriptor.Properties.Remove(ClientApplicationProperties.BackchannelLogoutUri);
            return;
        }

        descriptor.Properties[ClientApplicationProperties.BackchannelLogoutUri] =
            JsonSerializer.SerializeToElement(uri.AbsoluteUri);
    }

    public static Uri? GetBackchannelLogoutUri(this OpenIddictApplicationDescriptor descriptor)
    {
        if (descriptor.Properties.TryGetValue(
                ClientApplicationProperties.BackchannelLogoutUri, out JsonElement element)
            && element.GetString() is string value
            && Uri.TryCreate(value, UriKind.Absolute, out Uri? uri))
        {
            return uri;
        }

        return null;
    }

    /// <summary>
    /// Writes the client's declaration that its logout tokens must carry <c>sid</c>;
    /// <see langword="false"/> removes the property rather than storing a false.
    /// </summary>
    public static void SetBackchannelLogoutSessionRequired(
        this OpenIddictApplicationDescriptor descriptor, bool required)
    {
        if (!required)
        {
            descriptor.Properties.Remove(ClientApplicationProperties.BackchannelLogoutSessionRequired);
            return;
        }

        descriptor.Properties[ClientApplicationProperties.BackchannelLogoutSessionRequired] =
            JsonSerializer.SerializeToElement(true);
    }

    public static bool GetBackchannelLogoutSessionRequired(this OpenIddictApplicationDescriptor descriptor)
    {
        return descriptor.Properties.TryGetValue(
                ClientApplicationProperties.BackchannelLogoutSessionRequired, out JsonElement element)
            && element.ValueKind == JsonValueKind.True;
    }

    /// <summary>
    /// Writes the client's refresh-token lifetime as the OpenIddict per-application setting the
    /// server itself resolves at token creation — no Wallow handler reads it back on the token
    /// path. The setting value is an invariant-culture <see cref="TimeSpan"/> string; seconds are
    /// the unit the API speaks.
    /// </summary>
    public static void SetRefreshTokenLifetime(this OpenIddictApplicationDescriptor descriptor, int seconds)
    {
        descriptor.Settings[OpenIddictConstants.Settings.TokenLifetimes.RefreshToken] =
            ClientRefreshTokenLifetimes.ToSettingValue(seconds);
    }

    /// <summary>
    /// The client's refresh-token lifetime in whole seconds, or <see langword="null"/> when the
    /// client carries none and the global configuration decides.
    /// </summary>
    public static int? GetRefreshTokenLifetimeSeconds(this OpenIddictApplicationDescriptor descriptor)
    {
        return descriptor.Settings.TryGetValue(
            OpenIddictConstants.Settings.TokenLifetimes.RefreshToken, out string? setting)
            ? ClientRefreshTokenLifetimes.FromSettingValue(setting)
            : null;
    }
}
