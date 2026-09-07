using System.Text.Json;
using OpenIddict.Abstractions;
using Wallow.Identity.Application.Helpers;

namespace Wallow.Identity.Infrastructure.Extensions;

/// <summary>
/// Reads and writes client metadata using keys shared with the API-layer extensions.
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
    /// Converts seconds to the invariant TimeSpan setting OpenIddict reads when issuing refresh tokens.
    /// </summary>
    public static void SetRefreshTokenLifetime(this OpenIddictApplicationDescriptor descriptor, int seconds)
    {
        descriptor.Settings[OpenIddictConstants.Settings.TokenLifetimes.RefreshToken] =
            ClientRefreshTokenLifetimes.ToSettingValue(seconds);
    }

    /// <summary>
    /// Returns the stored refresh-token lifetime in whole seconds, or null if absent or unparseable.
    /// </summary>
    public static int? GetRefreshTokenLifetimeSeconds(this OpenIddictApplicationDescriptor descriptor)
    {
        return descriptor.Settings.TryGetValue(
            OpenIddictConstants.Settings.TokenLifetimes.RefreshToken, out string? setting)
            ? ClientRefreshTokenLifetimes.FromSettingValue(setting)
            : null;
    }
}
