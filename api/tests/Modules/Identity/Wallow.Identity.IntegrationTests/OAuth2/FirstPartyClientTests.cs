using System.Net;
using Wallow.Tests.Common.Factories;

namespace Wallow.Identity.IntegrationTests.OAuth2;

/// <summary>
/// First-party is a property the seed stamps on a client, never something the client id says.
/// A first-party client — whatever its id — signs a user in with no consent screen and no
/// organization; a lookalike id registered as anything else goes through consent like every
/// other third-party client.
/// </summary>
public sealed class FirstPartyClientTests(WallowApiFactory factory)
    : IdentityIntegrationTestBase(factory)
{
    private const string Password = "FirstParty1234!";
    private const string ClientSecret = "first-party-client-secret";
    private const string Scope = "openid profile email offline_access";
    private const string ConsentPath = "/consent";

    private static readonly string[] _clientScopes = ["openid", "profile", "email", "offline_access"];

    [Fact]
    public async Task Authorize_ForAFirstPartyClientWithAnOrdinaryIdAndNoOrganization_SignsInWithoutConsent()
    {
        string suffix = Guid.NewGuid().ToString("N");
        string email = $"first-party-{suffix}@wallow.dev";
        string clientId = $"platform-console-{suffix}";
        await AuthorizationCodeFlowHarness.CreateUserAsync(ScopedServices, email, Password);
        await AuthorizationCodeFlowHarness.RegisterClientAsync(
            ScopedServices, clientId, ClientSecret, tenantId: null, _clientScopes, firstParty: true);

        using AuthorizationCodeFlowHarness harness = new(Factory);
        await harness.SignInAsync(email, Password);

        TokenOutcome tokens = await harness.AcquireTokensAsync(clientId, ClientSecret, Scope);

        tokens.StatusCode.Should().Be(HttpStatusCode.OK, tokens.Body);
        AuthorizationCodeFlowHarness.ReadClaimValues(tokens.RequireAccessToken(), "org_id")
            .Should().BeEmpty("a first-party client is bound to no organization");
    }

    [Fact]
    public async Task Authorize_ForAFirstPartyClient_UserWithOneMembership_SignsInScopedToThatOrganization()
    {
        string suffix = Guid.NewGuid().ToString("N");
        string email = $"single-member-{suffix}@wallow.dev";
        string clientId = $"platform-console-{suffix}";
        Guid userId = await AuthorizationCodeFlowHarness.CreateUserAsync(ScopedServices, email, Password);
        Guid organizationId = await AuthorizationCodeFlowHarness.CreateOrganizationAsync(
            ScopedServices, $"Only Org {suffix}", userId);
        await AuthorizationCodeFlowHarness.RegisterClientAsync(
            ScopedServices, clientId, ClientSecret, tenantId: null, _clientScopes, firstParty: true);

        using AuthorizationCodeFlowHarness harness = new(Factory);
        await harness.SignInAsync(email, Password);

        TokenOutcome tokens = await harness.AcquireTokensAsync(clientId, ClientSecret, Scope);

        tokens.StatusCode.Should().Be(HttpStatusCode.OK, tokens.Body);
        AuthorizationCodeFlowHarness.ReadClaimValues(tokens.RequireAccessToken(), "org_id")
            .Should().ContainSingle()
            .Which.Should().Be(organizationId.ToString());
    }

    [Fact]
    public async Task Authorize_ForAThirdPartyClientWithAWallowPrefixedId_StillShowsTheConsentScreen()
    {
        string suffix = Guid.NewGuid().ToString("N");
        string email = $"lookalike-{suffix}@wallow.dev";
        string clientId = $"wallow-lookalike-{suffix}";
        Guid userId = await AuthorizationCodeFlowHarness.CreateUserAsync(ScopedServices, email, Password);
        Guid organizationId = await AuthorizationCodeFlowHarness.CreateOrganizationAsync(
            ScopedServices, $"Lookalike Org {suffix}", userId);
        await AuthorizationCodeFlowHarness.RegisterClientAsync(
            ScopedServices, clientId, ClientSecret, organizationId, _clientScopes);

        using AuthorizationCodeFlowHarness harness = new(Factory);
        await harness.SignInAsync(email, Password);

        AuthorizeOutcome authorize = await harness.AuthorizeAsync(clientId, Scope);

        authorize.Code.Should().BeNull(authorize.Location?.ToString());
        PathOf(authorize.Location).Should().Be(ConsentPath);
        authorize.ConsentToken.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Authorize_ForAThirdPartyClientBoundToNoOrganization_IsRefusedAsUnbound()
    {
        string suffix = Guid.NewGuid().ToString("N");
        string email = $"unbound-{suffix}@wallow.dev";
        string clientId = $"unbound-app-{suffix}";
        await AuthorizationCodeFlowHarness.CreateUserAsync(ScopedServices, email, Password);
        await AuthorizationCodeFlowHarness.RegisterClientAsync(
            ScopedServices, clientId, ClientSecret, tenantId: null, _clientScopes);

        using AuthorizationCodeFlowHarness harness = new(Factory);
        await harness.SignInAsync(email, Password);

        AuthorizeOutcome authorize = await harness.AuthorizeAsync(clientId, Scope);

        authorize.Code.Should().BeNull(authorize.Location?.ToString());
        authorize.Error.Should().Be("client_not_bound_to_organization");
    }

    /// <summary>The path a redirect lands on, absolute or relative to the API.</summary>
    private static string PathOf(Uri? location)
    {
        location.Should().NotBeNull("the endpoint should have redirected");
        string target = location.IsAbsoluteUri ? location.AbsolutePath : location.OriginalString;
        int query = target.IndexOf('?', StringComparison.Ordinal);
        return query >= 0 ? target[..query] : target;
    }
}
