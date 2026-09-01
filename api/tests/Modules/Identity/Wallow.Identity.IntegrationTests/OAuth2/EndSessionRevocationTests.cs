using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Wallow.Tests.Common.Factories;

namespace Wallow.Identity.IntegrationTests.OAuth2;

/// <summary>
/// End-session at the auth host ends the whole session, not just the cookie: every token minted
/// under the session's <c>sid</c> is revoked, so a refresh after logout answers
/// <c>invalid_grant</c> and the old access token is refused on its next bearer request. The
/// revocation is scoped to the one session — a second browser session of the same user, even
/// through the same client, keeps refreshing — which is exactly why tokens chain to a per-login
/// ad-hoc authorization rather than to the shared permanent consent record.
/// </summary>
public sealed class EndSessionRevocationTests(WallowApiFactory factory)
    : IdentityIntegrationTestBase(factory)
{
    private const string Password = "EndSession1234!";
    private const string ClientSecret = "end-session-secret";
    private const string Scope = "openid profile email offline_access";

    private static readonly string[] _clientScopes = ["openid", "profile", "email", "offline_access"];

    [Fact]
    public async Task Logout_KillsRefreshAndBearerAccess_ForTheSessionsTokens()
    {
        Seed seed = await SeedAsync();
        using AuthorizationCodeFlowHarness harness = await SignedInAsync(seed);
        TokenOutcome tokens = await ConsentedTokensAsync(harness, seed);

        // The positive baseline that keeps the 401 below honest: a live token passes validation.
        HttpResponseMessage preLogout = await BearerCallAsync(tokens.RequireAccessToken());
        preLogout.StatusCode.Should().Be(HttpStatusCode.OK);

        await LogoutAsync(harness);

        TokenOutcome refreshed = await harness.RefreshAsync(
            seed.ClientId, ClientSecret, tokens.RefreshToken!);
        refreshed.StatusCode.Should().Be(HttpStatusCode.BadRequest, refreshed.Body);
        refreshed.Error.Should().Be("invalid_grant");

        HttpResponseMessage bearerCall = await BearerCallAsync(tokens.RequireAccessToken());
        bearerCall.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_LeavesAnotherSessionOfTheSameUserAlive()
    {
        Seed seed = await SeedAsync();
        using AuthorizationCodeFlowHarness first = await SignedInAsync(seed);
        TokenOutcome firstTokens = await ConsentedTokensAsync(first, seed);

        using AuthorizationCodeFlowHarness second = new(Factory);
        await second.SignInAsync(seed.Email, Password);
        TokenOutcome secondTokens = await ConsentedTokensAsync(second, seed);

        await LogoutAsync(first);

        // The first session is dead...
        TokenOutcome firstRefreshed = await first.RefreshAsync(
            seed.ClientId, ClientSecret, firstTokens.RefreshToken!);
        firstRefreshed.Error.Should().Be("invalid_grant", firstRefreshed.Body);

        // ...and the second is untouched: its refresh grant still rotates, and its access token
        // still authorises a bearer call.
        TokenOutcome secondRefreshed = await second.RefreshAsync(
            seed.ClientId, ClientSecret, secondTokens.RefreshToken!);
        secondRefreshed.StatusCode.Should().Be(HttpStatusCode.OK, secondRefreshed.Body);

        HttpResponseMessage bearerCall = await BearerCallAsync(secondRefreshed.RequireAccessToken());
        bearerCall.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Logout_KillsAFirstPartySessionsTokens()
    {
        string suffix = Guid.NewGuid().ToString("N");
        string email = $"end-session-fp-{suffix}@wallow.dev";
        string clientId = $"end-session-console-{suffix}";
        Guid userId = await AuthorizationCodeFlowHarness.CreateUserAsync(ScopedServices, email, Password);
        await AuthorizationCodeFlowHarness.CreateOrganizationAsync(
            ScopedServices, $"End Session FP {suffix}", userId);
        await AuthorizationCodeFlowHarness.RegisterClientAsync(
            ScopedServices, clientId, ClientSecret, tenantId: null, _clientScopes, firstParty: true);

        using AuthorizationCodeFlowHarness harness = new(Factory);
        await harness.SignInAsync(email, Password);
        TokenOutcome tokens = await harness.AcquireTokensAsync(clientId, ClientSecret, Scope);
        tokens.StatusCode.Should().Be(HttpStatusCode.OK, tokens.Body);

        await LogoutAsync(harness);

        TokenOutcome refreshed = await harness.RefreshAsync(clientId, ClientSecret, tokens.RefreshToken!);
        refreshed.StatusCode.Should().Be(HttpStatusCode.BadRequest, refreshed.Body);
        refreshed.Error.Should().Be("invalid_grant");

        HttpResponseMessage bearerCall = await BearerCallAsync(tokens.RequireAccessToken());
        bearerCall.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_WithOnlyAnIdTokenHint_KillsTheSessionsTokens()
    {
        Seed seed = await SeedAsync();
        using AuthorizationCodeFlowHarness harness = await SignedInAsync(seed);
        TokenOutcome tokens = await ConsentedTokensAsync(harness, seed);

        // A cookie-less browser: the auth-host session cookie expired or was cleared before the
        // relying party sent the user to end-session with the id_token it still holds. The hint
        // is the only thing naming the session, and it must be enough to revoke its tokens.
        // Https, because hint validation derives the expected issuer from the request when no
        // explicit issuer is configured — over http the hint's https iss would never match.
        using HttpClient anonymous = Factory.CreateClient(
            new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        using HttpResponseMessage logout = await anonymous.GetAsync(new Uri(
            $"/connect/logout?id_token_hint={Uri.EscapeDataString(tokens.RequireIdToken())}&client_id={seed.ClientId}",
            UriKind.Relative));
        logout.StatusCode.Should().Be(HttpStatusCode.OK, await logout.Content.ReadAsStringAsync());

        TokenOutcome refreshed = await harness.RefreshAsync(
            seed.ClientId, ClientSecret, tokens.RefreshToken!);
        refreshed.StatusCode.Should().Be(HttpStatusCode.BadRequest, refreshed.Body);
        refreshed.Error.Should().Be("invalid_grant");

        HttpResponseMessage bearerCall = await BearerCallAsync(tokens.RequireAccessToken());
        bearerCall.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_WithAHintIssuedToAnotherClient_RevokesNothing()
    {
        Seed seed = await SeedAsync();
        string otherClientId = $"end-session-other-{Guid.NewGuid().ToString("N")}";
        await AuthorizationCodeFlowHarness.RegisterClientAsync(
            ScopedServices, otherClientId, ClientSecret, seed.OrganizationId, _clientScopes);

        using AuthorizationCodeFlowHarness harness = await SignedInAsync(seed);
        TokenOutcome tokens = await ConsentedTokensAsync(harness, seed);

        // A hint is only as good as the client presenting it: a different registered client
        // replaying someone else's id_token fails the audience check, so the session lives on.
        using HttpClient anonymous = Factory.CreateClient(
            new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        using HttpResponseMessage logout = await anonymous.GetAsync(new Uri(
            $"/connect/logout?id_token_hint={Uri.EscapeDataString(tokens.RequireIdToken())}&client_id={otherClientId}",
            UriKind.Relative));

        TokenOutcome refreshed = await harness.RefreshAsync(
            seed.ClientId, ClientSecret, tokens.RefreshToken!);
        refreshed.StatusCode.Should().Be(HttpStatusCode.OK, refreshed.Body);

        HttpResponseMessage bearerCall = await BearerCallAsync(refreshed.RequireAccessToken());
        bearerCall.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Logout_WithATamperedIdTokenHint_RevokesNothing()
    {
        Seed seed = await SeedAsync();
        using AuthorizationCodeFlowHarness harness = await SignedInAsync(seed);
        TokenOutcome tokens = await ConsentedTokensAsync(harness, seed);

        // A forged hint must never reach the revocation path: OpenIddict drops a hint whose
        // signature does not verify, so the session stays alive. Https like the honest-hint
        // test, so the only difference from the passing case is the broken signature.
        string tampered = string.Concat(tokens.RequireIdToken()[..^4], "AAAA");
        using HttpClient anonymous = Factory.CreateClient(
            new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        using HttpResponseMessage logout = await anonymous.GetAsync(new Uri(
            $"/connect/logout?id_token_hint={Uri.EscapeDataString(tampered)}&client_id={seed.ClientId}",
            UriKind.Relative));

        TokenOutcome refreshed = await harness.RefreshAsync(
            seed.ClientId, ClientSecret, tokens.RefreshToken!);
        refreshed.StatusCode.Should().Be(HttpStatusCode.OK, refreshed.Body);

        HttpResponseMessage bearerCall = await BearerCallAsync(refreshed.RequireAccessToken());
        bearerCall.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>Ends the session the way a browser does: a GET to the end-session endpoint.</summary>
    private static async Task LogoutAsync(AuthorizationCodeFlowHarness harness)
    {
        using HttpResponseMessage logout = await harness.Client.GetAsync(
            new Uri("/connect/logout", UriKind.Relative));
        ((int)logout.StatusCode).Should().BeLessThan(500, await logout.Content.ReadAsStringAsync());
    }

    /// <summary>Walks the explicit-consent flow to tokens for the seed's bound client.</summary>
    private async Task<TokenOutcome> ConsentedTokensAsync(AuthorizationCodeFlowHarness harness, Seed seed)
    {
        AuthorizeOutcome authorize = await harness.AuthorizeAsync(seed.ClientId, Scope);
        if (authorize.Code is null)
        {
            authorize = await harness.ConsentAsync(authorize, grant: true);
        }

        authorize.Code.Should().NotBeNull(authorize.Location?.ToString() ?? authorize.Body);
        TokenOutcome tokens = await harness.ExchangeCodeAsync(
            seed.ClientId, ClientSecret, authorize.Code!, authorize.CodeVerifier);
        tokens.StatusCode.Should().Be(HttpStatusCode.OK, tokens.Body);
        return tokens;
    }

    /// <summary>
    /// A bearer call that runs real JWT validation, not the test auth handler. Over https,
    /// because the validation handler refuses plain-http requests outright.
    /// </summary>
    private async Task<HttpResponseMessage> BearerCallAsync(string accessToken)
    {
        HttpClient bearer = Factory.CreateClient(
            new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        bearer.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");
        bearer.DefaultRequestHeaders.Add("X-Test-Auth-Skip", "true");
        return await bearer.GetAsync(new Uri("/identity/me/organizations", UriKind.Relative));
    }

    private async Task<AuthorizationCodeFlowHarness> SignedInAsync(Seed seed)
    {
        AuthorizationCodeFlowHarness harness = new(Factory);
        await harness.SignInAsync(seed.Email, Password);
        return harness;
    }

    private async Task<Seed> SeedAsync()
    {
        string suffix = Guid.NewGuid().ToString("N");
        string email = $"end-session-{suffix}@wallow.dev";

        Guid userId = await AuthorizationCodeFlowHarness.CreateUserAsync(ScopedServices, email, Password);
        Guid ownerId = await AuthorizationCodeFlowHarness.CreateUserAsync(
            ScopedServices, $"end-session-owner-{suffix}@wallow.dev", Password);
        Guid organizationId = await AuthorizationCodeFlowHarness.CreateOrganizationAsync(
            ScopedServices, $"End Session {suffix}", ownerId);
        await AuthorizationCodeFlowHarness.EnrollMemberAsync(
            ScopedServices, organizationId, userId, "user");

        string clientId = $"end-session-app-{suffix}";
        await AuthorizationCodeFlowHarness.RegisterClientAsync(
            ScopedServices, clientId, ClientSecret, organizationId, _clientScopes);

        return new Seed(email, clientId, userId, organizationId);
    }

    private sealed record Seed(string Email, string ClientId, Guid UserId, Guid OrganizationId);
}
