using System.Text.Json;
using OpenIddict.Abstractions;
using Wallow.Identity.Application.Helpers;

namespace Wallow.Identity.Infrastructure.Extensions;

/// <summary>
/// Reads authorization metadata using keys shared with the API-layer extensions.
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
    /// Matches OpenIddict ad-hoc authorization records using an ordinal type comparison.
    /// </summary>
    public static bool IsAdHocAuthorizationType(this string? type)
    {
        return string.Equals(type, OpenIddictConstants.AuthorizationTypes.AdHoc, StringComparison.Ordinal);
    }
}
