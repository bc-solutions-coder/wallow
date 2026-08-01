using System.Collections.Immutable;
using System.Security.Claims;
using OpenIddict.Abstractions;
using Wallow.Identity.Api.Userinfo;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Wallow.Identity.Tests.Api.Userinfo;

/// <summary>
/// The userinfo body is scope-gated: a relying party sees only what the scopes it was granted
/// cover. <c>org_id</c>/<c>org_name</c> ride with <c>profile</c>.
/// </summary>
public sealed class UserinfoClaimsTests
{
    private static readonly string _orgId = Guid.NewGuid().ToString();

    [Fact]
    public void Project_WithProfileScope_ReportsTheOrganization()
    {
        ClaimsPrincipal principal = Principal([Scopes.OpenId, Scopes.Profile]);

        Dictionary<string, object> claims = UserinfoClaims.Project(principal);

        claims.Should().Contain("org_id", _orgId);
        claims.Should().Contain("org_name", "Contoso");
    }

    [Fact]
    public void Project_WithoutProfileScope_OmitsTheOrganization()
    {
        ClaimsPrincipal principal = Principal([Scopes.OpenId, Scopes.Email]);

        Dictionary<string, object> claims = UserinfoClaims.Project(principal);

        claims.Should().NotContainKey("org_id");
        claims.Should().NotContainKey("org_name");
        claims.Should().ContainKey(Claims.Email);
    }

    [Fact]
    public void Project_ForAUserWhoseOrganizationHasNoName_OmitsOnlyTheName()
    {
        ClaimsPrincipal principal = Principal([Scopes.Profile], orgName: null);

        Dictionary<string, object> claims = UserinfoClaims.Project(principal);

        claims.Should().Contain("org_id", _orgId);
        claims.Should().NotContainKey("org_name");
    }

    [Fact]
    public void Project_AlwaysReportsTheSubject()
    {
        ClaimsPrincipal principal = Principal([]);

        Dictionary<string, object> claims = UserinfoClaims.Project(principal);

        claims.Should().Contain(Claims.Subject, "user-1");
        claims.Should().HaveCount(1);
    }

    [Fact]
    public void Project_WithRolesScope_ReportsTheRolesTheOrganizationGranted()
    {
        ClaimsPrincipal principal = Principal([Scopes.Roles]);

        Dictionary<string, object> claims = UserinfoClaims.Project(principal);

        claims[Claims.Role].Should().BeEquivalentTo(ImmutableArray.Create("admin", "user"));
    }

    private static ClaimsPrincipal Principal(string[] scopes, string? orgName = "Contoso")
    {
        ClaimsIdentity identity = new("Test");
        identity.AddClaim(new Claim(Claims.Subject, "user-1"));
        identity.AddClaim(new Claim(Claims.Name, "Ada Lovelace"));
        identity.AddClaim(new Claim(Claims.Email, "ada@example.com"));
        identity.AddClaim(new Claim(Claims.Role, "admin"));
        identity.AddClaim(new Claim(Claims.Role, "user"));
        identity.AddClaim(new Claim("org_id", _orgId));

        if (orgName is not null)
        {
            identity.AddClaim(new Claim("org_name", orgName));
        }

        identity.SetScopes(scopes);

        return new ClaimsPrincipal(identity);
    }
}
