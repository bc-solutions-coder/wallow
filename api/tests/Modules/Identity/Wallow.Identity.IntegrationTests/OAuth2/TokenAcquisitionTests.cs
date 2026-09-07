using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Wallow.Tests.Common.Factories;

namespace Wallow.Identity.IntegrationTests.OAuth2;

/// <summary>
/// Checks client-credentials acquisition and organization-role isolation in refreshed tokens.
/// </summary>
[Trait("Category", "Integration")]
public class TokenAcquisitionTests(WallowApiFactory factory) : IdentityIntegrationTestBase(factory)
{
    private const string Password = "Harness1234!";
    private const string RefreshClientSecret = "refresh-client-secret";

    [Fact]
    public async Task Refresh_ForAUserWhoIsAdminElsewhere_ReissuesOnlyThisOrganizationsRoles()
    {
        string suffix = Guid.NewGuid().ToString("N");
        string email = $"refresh-{suffix}@wallow.dev";

        // Ownership grants admin in A; that role must not appear in the token for B.
        Guid userId = await AuthorizationCodeFlowHarness.CreateUserAsync(ScopedServices, email, Password);
        await AuthorizationCodeFlowHarness.CreateOrganizationAsync(
            ScopedServices, $"Refresh Org A {suffix}", userId);

        Guid outsiderId = await AuthorizationCodeFlowHarness.CreateUserAsync(
            ScopedServices, $"refresh-owner-{suffix}@wallow.dev", Password);
        Guid organizationB = await AuthorizationCodeFlowHarness.CreateOrganizationAsync(
            ScopedServices, $"Refresh Org B {suffix}", outsiderId);
        await AuthorizationCodeFlowHarness.EnrollMemberAsync(
            ScopedServices, organizationB, userId, "user");

        string clientId = $"wallow-refresh-{suffix}";
        await AuthorizationCodeFlowHarness.RegisterClientAsync(
            ScopedServices,
            clientId,
            RefreshClientSecret,
            tenantId: null,
            ["openid", "profile", "email", "roles", "offline_access"],
            firstParty: true);

        using AuthorizationCodeFlowHarness harness = new(Factory);
        await harness.SignInAsync(email, Password);

        TokenOutcome issued = await harness.AcquireTokensAsync(
            clientId, RefreshClientSecret, "openid profile email roles offline_access",
            organization: organizationB.ToString());
        issued.RefreshToken.Should().NotBeNull(issued.Body);

        TokenOutcome refreshed = await harness.RefreshAsync(
            clientId, RefreshClientSecret, issued.RefreshToken!);
        refreshed.StatusCode.Should().Be(HttpStatusCode.OK, refreshed.Body);

        IReadOnlyList<string> roles = AuthorizationCodeFlowHarness.ReadClaimValues(
            refreshed.RequireAccessToken(), "role");
        roles.Should().Contain("user");
        roles.Should().NotContain("admin");
    }

    [Fact]
    public async Task Should_Acquire_Token_With_Client_Credentials()
    {
        string? token = await RequestClientCredentialsTokenAsync(
            IdentityFixture.ApiClientId,
            IdentityFixture.ApiClientSecret);

        token.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Should_Fail_With_Invalid_Client_Secret()
    {
        HttpResponseMessage response = await PostTokenRequestAsync(
            IdentityFixture.ApiClientId,
            "invalid-secret");

        response.StatusCode.Should().NotBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Should_Fail_With_Invalid_Client_Id()
    {
        HttpResponseMessage response = await PostTokenRequestAsync(
            "invalid-client",
            IdentityFixture.ApiClientSecret);

        response.StatusCode.Should().NotBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Test_Service_Account_Should_Acquire_Token()
    {
        string? token = await RequestClientCredentialsTokenAsync(
            IdentityFixture.ServiceAccountClientId,
            IdentityFixture.ServiceAccountClientSecret);

        token.Should().NotBeNullOrWhiteSpace();
    }

    private async Task<string?> RequestClientCredentialsTokenAsync(string clientId, string clientSecret)
    {
        HttpResponseMessage response = await PostTokenRequestAsync(clientId, clientSecret);
        response.EnsureSuccessStatusCode();

        TokenResponse? content = await response.Content.ReadFromJsonAsync<TokenResponse>();
        return content?.AccessToken;
    }

    private async Task<HttpResponseMessage> PostTokenRequestAsync(string clientId, string clientSecret)
    {
        // Send the token request without synthetic authentication headers.
        HttpClient tokenClient = Factory.CreateClient();
        tokenClient.DefaultRequestHeaders.Remove("Authorization");

        using FormUrlEncodedContent content = new(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
            ["scope"] = "openid"
        });
        return await tokenClient.PostAsync("/connect/token", content);
    }

    private sealed record TokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; init; } = string.Empty;
    }
}
