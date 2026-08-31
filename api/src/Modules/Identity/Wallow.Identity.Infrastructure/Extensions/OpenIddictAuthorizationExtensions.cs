using System.Text.Json;
using OpenIddict.Abstractions;
using Wallow.Identity.Application.Helpers;

namespace Wallow.Identity.Infrastructure.Extensions;

/// <summary>
/// Reads Wallow-defined properties off an authorization record. The Api layer writes them through
/// its own twin; both address the property through
/// <see cref="AuthorizationProperties.OrganizationId"/>.
/// </summary>
public static class OpenIddictAuthorizationExtensions
{
    public static Guid? GetOrganizationId(this OpenIddictAuthorizationDescriptor descriptor)
    {
        if (descriptor.Properties.TryGetValue(AuthorizationProperties.OrganizationId, out JsonElement element)
            && Guid.TryParse(element.GetString(), out Guid organizationId))
        {
            return organizationId;
        }

        return null;
    }

    public static string? GetSessionId(this OpenIddictAuthorizationDescriptor descriptor)
    {
        return descriptor.Properties.TryGetValue(AuthorizationProperties.SessionId, out JsonElement element)
            ? element.GetString()
            : null;
    }

    /// <summary>
    /// True when <paramref name="type"/> is OpenIddict's ad-hoc authorization type — the
    /// per-login rows sign-ins mint, as opposed to permanent consent records. OpenIddict writes
    /// the constant verbatim, so the comparison is ordinal. The one predicate every revocation
    /// walk shares, so no two walks can diverge on how they read the discriminator.
    /// </summary>
    public static bool IsAdHocAuthorizationType(this string? type)
    {
        return string.Equals(type, OpenIddictConstants.AuthorizationTypes.AdHoc, StringComparison.Ordinal);
    }
}
