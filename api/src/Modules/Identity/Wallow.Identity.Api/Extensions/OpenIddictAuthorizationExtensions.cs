using System.Text.Json;
using OpenIddict.Abstractions;
using Wallow.Identity.Application.Helpers;

namespace Wallow.Identity.Api.Extensions;

/// <summary>
/// Writes organization and session metadata using keys shared with infrastructure readers.
/// </summary>
public static class OpenIddictAuthorizationExtensions
{
    public static void SetOrganizationId(this OpenIddictAuthorizationDescriptor descriptor, Guid organizationId)
    {
        descriptor.Properties[AuthorizationProperties.OrganizationId] =
            JsonSerializer.SerializeToElement(organizationId.ToString());
    }

    public static void SetSessionId(this OpenIddictAuthorizationDescriptor descriptor, string sessionId)
    {
        descriptor.Properties[AuthorizationProperties.SessionId] =
            JsonSerializer.SerializeToElement(sessionId);
    }
}
