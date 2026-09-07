using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Wallow.Tests.Common.Factories;

namespace Wallow.Identity.IntegrationTests.OAuth2;

/// <summary>
/// Checks consent listing and withdrawal for the authenticated user.
/// Withdrawal removes the listed consent and rejects subsequent refresh and bearer requests.
/// </summary>
public sealed class ConnectedApplicationTests(WallowApiFactory factory)
    : IdentityIntegrationTestBase(factory)
{
    private const string Password = "Harness1234!";
    private const string ClientSecret = "connected-app-secret";
    /// <summary>
    /// Requests offline access so withdrawal can be checked with a refresh grant.
    /// </summary>
    private const string Scope = "openid profile email offline_access";

    private static readonly string[] _clientScopes = ["openid", "profile", "email", "offline_access"];

    [Fact]
    public async Task List_NamesTheConsentedApplicationWithItsScopes()
    {
        Seed seed = await SeedAsync();
        using AuthorizationCodeFlowHarness harness = await SignedInAsync(seed);
        AuthorizeOutcome consent = await harness.AuthorizeAsync(seed.ClientId, Scope);
        AuthorizeOutcome granted = await harness.ConsentAsync(consent, grant: true);
        granted.Code.Should().NotBeNull(granted.Location?.ToString() ?? granted.Body);

        SetTestUser(seed.UserId.ToString(), "user");
        HttpResponseMessage response = await Client.GetAsync(
            new Uri("/identity/me/authorizations", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonElement list = await response.Content.ReadFromJsonAsync<JsonElement>();
        JsonElement entry = list.EnumerateArray().Should().ContainSingle().Subject;
        entry.GetProperty("clientId").GetString().Should().Be(seed.ClientId);
        entry.GetProperty("displayName").GetString().Should().Be(seed.ClientId);
        entry.GetProperty("id").GetString().Should().NotBeNullOrEmpty();
        entry.GetProperty("scopes").EnumerateArray().Select(s => s.GetString())
            .Should().BeEquivalentTo("openid", "profile", "email", "offline_access");
    }

    [Fact]
    public async Task List_IsEmptyForAUserWhoConsentedToNothing()
    {
        Seed seed = await SeedAsync();

        SetTestUser(seed.UserId.ToString(), "user");
        HttpResponseMessage response = await Client.GetAsync(
            new Uri("/identity/me/authorizations", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonElement list = await response.Content.ReadFromJsonAsync<JsonElement>();
        list.EnumerateArray().Should().BeEmpty();
    }

    [Fact]
    public async Task Withdraw_KillsRefreshAndBearerAccess_AndEmptiesTheList()
    {
        Seed seed = await SeedAsync();
        using AuthorizationCodeFlowHarness harness = await SignedInAsync(seed);
        AuthorizeOutcome consent = await harness.AuthorizeAsync(seed.ClientId, Scope);
        AuthorizeOutcome granted = await harness.ConsentAsync(consent, grant: true);
        TokenOutcome tokens = await harness.ExchangeCodeAsync(
            seed.ClientId, ClientSecret, granted.Code!, granted.CodeVerifier);
        tokens.StatusCode.Should().Be(HttpStatusCode.OK, tokens.Body);

        SetTestUser(seed.UserId.ToString(), "user");
        string authorizationId = await SingleAuthorizationIdAsync();

        HttpResponseMessage withdraw = await Client.DeleteAsync(
            new Uri($"/identity/me/authorizations/{Uri.EscapeDataString(authorizationId)}", UriKind.Relative));
        withdraw.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Withdrawing consent must also revoke the session authorization used by this refresh token.
        TokenOutcome refreshed = await harness.RefreshAsync(
            seed.ClientId, ClientSecret, tokens.RefreshToken!);
        refreshed.StatusCode.Should().Be(HttpStatusCode.BadRequest, refreshed.Body);
        refreshed.Error.Should().Be("invalid_grant");

        // Use real bearer validation over HTTPS to check rejection after withdrawal.
        HttpClient bearer = Factory.CreateClient(
            new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        bearer.DefaultRequestHeaders.Add("Authorization", $"Bearer {tokens.RequireAccessToken()}");
        bearer.DefaultRequestHeaders.Add("X-Test-Auth-Skip", "true");
        HttpResponseMessage bearerCall = await bearer.GetAsync(
            new Uri("/identity/me/organizations", UriKind.Relative));
        bearerCall.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        HttpResponseMessage after = await Client.GetAsync(
            new Uri("/identity/me/authorizations", UriKind.Relative));
        JsonElement list = await after.Content.ReadFromJsonAsync<JsonElement>();
        list.EnumerateArray().Should().BeEmpty();
    }

    [Fact]
    public async Task Withdraw_OfSomeoneElsesAuthorization_IsNotFoundAndChangesNothing()
    {
        Seed seed = await SeedAsync();
        using AuthorizationCodeFlowHarness harness = await SignedInAsync(seed);
        AuthorizeOutcome consent = await harness.AuthorizeAsync(seed.ClientId, Scope);
        AuthorizeOutcome granted = await harness.ConsentAsync(consent, grant: true);
        granted.Code.Should().NotBeNull(granted.Location?.ToString() ?? granted.Body);

        SetTestUser(seed.UserId.ToString(), "user");
        string authorizationId = await SingleAuthorizationIdAsync();

        Seed stranger = await SeedAsync();
        SetTestUser(stranger.UserId.ToString(), "user");
        HttpResponseMessage foreignWithdraw = await Client.DeleteAsync(
            new Uri($"/identity/me/authorizations/{Uri.EscapeDataString(authorizationId)}", UriKind.Relative));
        foreignWithdraw.StatusCode.Should().Be(HttpStatusCode.NotFound);

        SetTestUser(seed.UserId.ToString(), "user");
        string stillThere = await SingleAuthorizationIdAsync();
        stillThere.Should().Be(authorizationId);
    }

    [Fact]
    public async Task Withdraw_OfAnUnknownAuthorization_IsNotFound()
    {
        Seed seed = await SeedAsync();

        SetTestUser(seed.UserId.ToString(), "user");
        HttpResponseMessage response = await Client.DeleteAsync(
            new Uri("/identity/me/authorizations/no-such-authorization", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>The one authorization the signed-in test user's list names, by id.</summary>
    private async Task<string> SingleAuthorizationIdAsync()
    {
        HttpResponseMessage response = await Client.GetAsync(
            new Uri("/identity/me/authorizations", UriKind.Relative));
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonElement list = await response.Content.ReadFromJsonAsync<JsonElement>();
        JsonElement entry = list.EnumerateArray().Should().ContainSingle().Subject;
        return entry.GetProperty("id").GetString()!;
    }

    private async Task<AuthorizationCodeFlowHarness> SignedInAsync(Seed seed)
    {
        await AuthorizationCodeFlowHarness.EnrollMemberAsync(
            ScopedServices, seed.OrganizationId, seed.UserId, "user");

        AuthorizationCodeFlowHarness harness = new(Factory);
        await harness.SignInAsync(seed.Email, Password);
        return harness;
    }

    private async Task<Seed> SeedAsync()
    {
        string suffix = Guid.NewGuid().ToString("N");
        string email = $"connected-{suffix}@wallow.dev";

        Guid userId = await AuthorizationCodeFlowHarness.CreateUserAsync(ScopedServices, email, Password);
        Guid ownerId = await AuthorizationCodeFlowHarness.CreateUserAsync(
            ScopedServices, $"connected-owner-{suffix}@wallow.dev", Password);
        Guid organizationId = await AuthorizationCodeFlowHarness.CreateOrganizationAsync(
            ScopedServices, $"Connected {suffix}", ownerId);

        string clientId = $"connected-app-{suffix}";
        await AuthorizationCodeFlowHarness.RegisterClientAsync(
            ScopedServices, clientId, ClientSecret, organizationId, _clientScopes);

        return new Seed(email, clientId, userId, organizationId);
    }

    private sealed record Seed(string Email, string ClientId, Guid UserId, Guid OrganizationId);
}
