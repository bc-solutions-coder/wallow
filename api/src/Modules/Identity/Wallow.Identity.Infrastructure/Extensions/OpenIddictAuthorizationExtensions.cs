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
}
