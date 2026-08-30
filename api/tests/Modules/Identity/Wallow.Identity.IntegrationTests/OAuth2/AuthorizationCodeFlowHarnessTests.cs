using System.Net;
using Wallow.Tests.Common.Factories;

namespace Wallow.Identity.IntegrationTests.OAuth2;

/// <summary>
/// Proves the harness drives a real authorization-code flow: cookie sign-in, PKCE authorize, code
/// exchange and refresh, plus the membership refusal, so the specs built on it fail for their own
/// reasons rather than because the flow never ran.
/// </summary>
public sealed class AuthorizationCodeFlowHarnessTests(WallowApiFactory factory)
    : IdentityIntegrationTestBase(factory)
{
    private const string Password = "Harness1234!";
    private const string ClientSecret = "harness-client-secret";
    private const string Scope = "openid profile email offline_access";

    private static readonly string[] _clientScopes = ["openid", "profile", "email", "offline_access"];

    [Fact]
    public async Task AcquireTokens_ForAMemberOfTheClientsOrganization_IssuesATokenScopedToThatOrganization()
    {
        (string email, Guid organizationId, string clientId) = await SeedAsync();

        using AuthorizationCodeFlowHarness harness = new(Factory);
        await harness.SignInAsync(email, Password);

        TokenOutcome tokens = await harness.AcquireTokensAsync(
            clientId, ClientSecret, Scope, organization: organizationId.ToString());

        tokens.StatusCode.Should().Be(HttpStatusCode.OK, tokens.Body);
        AuthorizationCodeFlowHarness.ReadClaimValues(tokens.RequireAccessToken(), "org_id")
            .Should().ContainSingle()
            .Which.Should().Be(organizationId.ToString());
    }

    [Fact]
    public async Task Refresh_AfterAcquiringTokens_IssuesAFreshAccessToken()
    {
        (string email, Guid organizationId, string clientId) = await SeedAsync();

        using AuthorizationCodeFlowHarness harness = new(Factory);
        await harness.SignInAsync(email, Password);

        TokenOutcome tokens = await harness.AcquireTokensAsync(
            clientId, ClientSecret, Scope, organization: organizationId.ToString());
        tokens.RefreshToken.Should().NotBeNull(tokens.Body);

        TokenOutcome refreshed = await harness.RefreshAsync(clientId, ClientSecret, tokens.RefreshToken!);

        refreshed.StatusCode.Should().Be(HttpStatusCode.OK, refreshed.Body);
        AuthorizationCodeFlowHarness.ReadClaimValues(refreshed.RequireAccessToken(), "org_id")
            .Should().ContainSingle()
            .Which.Should().Be(organizationId.ToString());
    }

    [Fact]
    public async Task Authorize_ForAUserOutsideTheClientsOrganization_IssuesNoCode()
    {
        (_, Guid organizationId, string clientId) = await SeedAsync();
        string outsiderEmail = $"harness-outsider-{Guid.NewGuid():N}@wallow.dev";
        await AuthorizationCodeFlowHarness.CreateUserAsync(ScopedServices, outsiderEmail, Password);

        using AuthorizationCodeFlowHarness harness = new(Factory);
        await harness.SignInAsync(outsiderEmail, Password);

        AuthorizeOutcome authorize = await harness.AuthorizeAsync(
            clientId, Scope, organization: organizationId.ToString());

        authorize.Code.Should().BeNull(authorize.Location?.ToString());
        authorize.Error.Should().Be("not_a_member");
    }

    private async Task<(string Email, Guid OrganizationId, string ClientId)> SeedAsync()
    {
        string suffix = Guid.NewGuid().ToString("N");
        string email = $"harness-{suffix}@wallow.dev";

        Guid userId = await AuthorizationCodeFlowHarness.CreateUserAsync(ScopedServices, email, Password);
        Guid organizationId = await AuthorizationCodeFlowHarness.CreateOrganizationAsync(
            ScopedServices,
            $"Harness Org {suffix}",
            userId);

        string clientId = $"wallow-harness-{suffix}";
        await AuthorizationCodeFlowHarness.RegisterClientAsync(
            ScopedServices,
            clientId,
            ClientSecret,
            tenantId: null,
            _clientScopes,
            firstParty: true);

        return (email, organizationId, clientId);
    }
}
