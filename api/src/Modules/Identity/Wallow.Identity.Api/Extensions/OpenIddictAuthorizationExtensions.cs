using System.Text.Json;
using OpenIddict.Abstractions;
using Wallow.Identity.Application.Helpers;

namespace Wallow.Identity.Api.Extensions;

/// <summary>
/// Writes Wallow-defined properties on an authorization record. The Infrastructure layer reads
/// them back through its own twin; both address the property through
/// <see cref="AuthorizationProperties.OrganizationId"/>.
/// </summary>
public static class OpenIddictAuthorizationExtensions
{
    public static void SetOrganizationId(this OpenIddictAuthorizationDescriptor descriptor, Guid organizationId)
    {
        descriptor.Properties[AuthorizationProperties.OrganizationId] =
            JsonSerializer.SerializeToElement(organizationId.ToString());
    }
}
