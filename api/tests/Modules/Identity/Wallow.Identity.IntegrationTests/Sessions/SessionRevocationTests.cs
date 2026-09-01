using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Wallow.Identity.IntegrationTests.OAuth2;
using Wallow.Tests.Common.Factories;

namespace Wallow.Identity.IntegrationTests.Sessions;

/// <summary>
/// The sessions API operates on real sign-ins: authorize writes an <c>ActiveSession</c> ledger
/// row whose id doubles as the session's OIDC <c>sid</c>, so the list shows the session the
/// id_token names, and DELETE on that row revokes the session's tokens — not just the ledger
/// flag. A refresh afterwards answers <c>invalid_grant</c> and the old access token is refused.
/// </summary>
public sealed class SessionRevocationTests(WallowApiFactory factory)
    : IdentityIntegrationTestBase(factory)
{
    private const string Password = "SessionLedger1234!";
    private const string ClientSecret = "session-ledger-secret";
    private const string Scope = "openid profile email offline_access";

    private static readonly string[] _clientScopes = ["openid", "profile", "email", "offline_access"];

    [Fact]
    public async Task SigningIn_ListsTheSessionUnderItsIdTokenSid()
    {
        Seed seed = await SeedAsync();
        using AuthorizationCodeFlowHarness harness = await SignedInAsync(seed);
        TokenOutcome tokens = await AcquireTokensAsync(harness, seed);
        string sid = ReadSid(tokens);

        using JsonDocument sessions = await ListSessionsAsync(seed.UserId);
        IEnumerable<string> listedSids = sessions.RootElement.EnumerateArray()
            .Select(s => s.GetProperty("id").GetGuid().ToString("N"));

        listedSids.Should().Contain(sid);
    }

    [Fact]
    public async Task DeletingASession_KillsRefreshAndBearerAccess()
    {
        Seed seed = await SeedAsync();
        using AuthorizationCodeFlowHarness harness = await SignedInAsync(seed);
        TokenOutcome tokens = await AcquireTokensAsync(harness, seed);
        Guid sessionId = Guid.ParseExact(ReadSid(tokens), "N");

        // The positive baseline that keeps the 401 below honest: a live token passes validation.
        HttpResponseMessage preDelete = await BearerCallAsync(tokens.RequireAccessToken());
        preDelete.StatusCode.Should().Be(HttpStatusCode.OK);

        using HttpClient client = ActingClient(seed.UserId);
        using HttpResponseMessage deleted = await client.DeleteAsync(new Uri(
            $"/v1/identity/sessions/{sessionId}", UriKind.Relative));
        deleted.StatusCode.Should().Be(HttpStatusCode.NoContent, await deleted.Content.ReadAsStringAsync());

        TokenOutcome refreshed = await harness.RefreshAsync(
            seed.ClientId, ClientSecret, tokens.RefreshToken!);
        refreshed.StatusCode.Should().Be(HttpStatusCode.BadRequest, refreshed.Body);
        refreshed.Error.Should().Be("invalid_grant");

        HttpResponseMessage bearerCall = await BearerCallAsync(tokens.RequireAccessToken());
        bearerCall.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeletingASession_TheNextAuthorizeStartsAFreshSession()
    {
        Seed seed = await SeedAsync();
        using AuthorizationCodeFlowHarness harness = await SignedInAsync(seed);
        TokenOutcome tokens = await AcquireTokensAsync(harness, seed);
        string deadSid = ReadSid(tokens);

        using HttpClient client = ActingClient(seed.UserId);
        using HttpResponseMessage deleted = await client.DeleteAsync(new Uri(
            $"/v1/identity/sessions/{Guid.ParseExact(deadSid, "N")}", UriKind.Relative));
        deleted.StatusCode.Should().Be(HttpStatusCode.NoContent, await deleted.Content.ReadAsStringAsync());

        // The browser cookie survives the delete, but the next authorize must not resurrect the
        // revoked sid: it starts a fresh session — listed, and separately revocable — instead of
        // silently minting new tokens under the row the admin already killed.
        TokenOutcome fresh = await AcquireTokensAsync(harness, seed);
        string freshSid = ReadSid(fresh);
        freshSid.Should().NotBe(deadSid);

        using JsonDocument sessions = await ListSessionsAsync(seed.UserId);
        List<string> listedSids = sessions.RootElement.EnumerateArray()
            .Select(s => s.GetProperty("id").GetGuid().ToString("N"))
            .ToList();
        listedSids.Should().Contain(freshSid);
        listedSids.Should().NotContain(deadSid);
    }

    /// <summary>The sid the id_token carries — the ledger row id in "N" format.</summary>
    private static string ReadSid(TokenOutcome tokens)
    {
        JsonElement payload = AuthorizationCodeFlowHarness.ReadPayload(tokens.RequireIdToken());
        string? sid = payload.GetProperty("sid").GetString();
        sid.Should().NotBeNullOrEmpty();
        return sid!;
    }

    private async Task<AuthorizationCodeFlowHarness> SignedInAsync(Seed seed)
    {
        AuthorizationCodeFlowHarness harness = new(Factory);
        await harness.SignInAsync(seed.Email, Password);
        return harness;
    }

    private static async Task<TokenOutcome> AcquireTokensAsync(AuthorizationCodeFlowHarness harness, Seed seed)
    {
        TokenOutcome tokens = await harness.AcquireTokensAsync(seed.ClientId, ClientSecret, Scope);
        tokens.StatusCode.Should().Be(HttpStatusCode.OK, tokens.Body);
        return tokens;
    }

    private async Task<JsonDocument> ListSessionsAsync(Guid userId)
    {
        using HttpClient client = ActingClient(userId);
        using HttpResponseMessage response = await client.GetAsync(
            new Uri("/v1/identity/sessions", UriKind.Relative));
        string body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, body);
        return JsonDocument.Parse(body);
    }

    /// <summary>A client the test auth handler authenticates as the given user.</summary>
    private HttpClient ActingClient(Guid userId)
    {
        HttpClient client = Factory.CreateClient();
        client.DefaultRequestHeaders.Add("Authorization", "Bearer test-token");
        client.DefaultRequestHeaders.Add("X-Test-User-Id", userId.ToString());
        client.DefaultRequestHeaders.Add("X-Test-Roles", "user");
        return client;
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

    private async Task<Seed> SeedAsync()
    {
        string suffix = Guid.NewGuid().ToString("N");
        string email = $"session-ledger-{suffix}@wallow.dev";

        Guid userId = await AuthorizationCodeFlowHarness.CreateUserAsync(ScopedServices, email, Password);
        await AuthorizationCodeFlowHarness.CreateOrganizationAsync(
            ScopedServices, $"Session Ledger {suffix}", userId);

        string clientId = $"session-ledger-app-{suffix}";
        await AuthorizationCodeFlowHarness.RegisterClientAsync(
            ScopedServices, clientId, ClientSecret, tenantId: null, _clientScopes, firstParty: true);

        return new Seed(email, clientId, userId);
    }

    private sealed record Seed(string Email, string ClientId, Guid UserId);
}
