using System.Collections.Immutable;
using System.Security.Claims;
using OpenIddict.Abstractions;
using Wallow.Shared.Kernel.Extensions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Wallow.Identity.Api.Userinfo;

/// <summary>
/// Projects an authenticated principal onto the userinfo response body.
/// </summary>
public static class UserinfoClaims
{
    private const string OrgIdClaim = "org_id";
    private const string OrgNameClaim = "org_name";

    /// <summary>
    /// Builds the userinfo response for <paramref name="principal"/>, including only the claims
    /// the scopes it was granted cover.
    /// <para>
    /// The organization a user is signed in as travels under <c>profile</c>. Which org the
    /// session belongs to is a fact about the user this token describes, and no scope in the API
    /// catalog answers it; a relying party that asks for the profile is the one that needs it.
    /// </para>
    /// </summary>
    /// <param name="principal">The principal the userinfo request authenticated as.</param>
    /// <returns>The claim names and values to serialize, keyed by claim name.</returns>
    public static Dictionary<string, object> Project(ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);

        Dictionary<string, object> claims = new(StringComparer.Ordinal)
        {
            [Claims.Subject] = principal.GetClaim(Claims.Subject)!
        };

        if (principal.HasScope(Scopes.Profile))
        {
            AddIfPresent(claims, Claims.Name, principal.GetClaim(Claims.Name));
            AddIfPresent(claims, Claims.GivenName, principal.GetClaim(Claims.GivenName));
            AddIfPresent(claims, Claims.FamilyName, principal.GetClaim(Claims.FamilyName));
            AddIfPresent(claims, OrgIdClaim, principal.GetTenantId());
            AddIfPresent(claims, OrgNameClaim, principal.GetTenantName());
        }

        if (principal.HasScope(Scopes.Email))
        {
            AddIfPresent(claims, Claims.Email, principal.GetClaim(Claims.Email));
        }

        if (principal.HasScope(Scopes.Roles))
        {
            ImmutableArray<string> roles = [.. principal.GetClaims(Claims.Role)];
            if (roles.Length > 0)
            {
                claims[Claims.Role] = roles;
            }
        }

        return claims;
    }

    private static void AddIfPresent(Dictionary<string, object> claims, string name, string? value)
    {
        if (value is not null)
        {
            claims[name] = value;
        }
    }
}
