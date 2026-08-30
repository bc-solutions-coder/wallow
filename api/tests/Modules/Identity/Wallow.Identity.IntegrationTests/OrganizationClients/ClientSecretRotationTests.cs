using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wallow.Identity.IntegrationTests.OAuth2;
using Wallow.Shared.Infrastructure.Core.Auditing;
using Wallow.Tests.Common.Factories;

namespace Wallow.Identity.IntegrationTests.OrganizationClients;

/// <summary>
/// Secret rotation on the org-scoped client surface: the new secret is revealed once and works
/// immediately, the old one stops working at the same moment, and the optional
/// <c>revokeActiveTokens</c> flag ends every token the client was already issued. Proven against
/// the real token endpoint and, for bearer calls, the real validation handler (the stub scheme is
/// bypassed per request).
/// </summary>
[Trait("Category", "Integration")]
public class ClientSecretRotationTests(WallowApiFactory factory) : IdentityIntegrationTestBase(factory)
{
    private const string MemberPassword = "Member1234!";
    private static readonly string[] _applicationScopes = ["openid", "offline_access"];
    private static readonly string[] _readScopes = ["organizations.read"];

    [Fact]
    public async Task Rotate_RevealsANewSecretOnce_AndTheOldOneStopsWorkingImmediately()
    {
        Guid orgId = await OrganizationOwnedBySomeoneElseAsync("Rotation Org");
        await ActAsEnrolledAsync(orgId, "manager");
        (string clientId, string oldSecret) = await RegisterServiceAccountAsync(orgId, "Nightly sync");
        (await ClientCredentialsAsync(clientId, oldSecret)).StatusCode.Should().Be(HttpStatusCode.OK);

        HttpResponseMessage response = await Client.PostAsJsonAsync(
            $"/identity/organizations/{orgId}/clients/{clientId}/rotate-secret",
            new { revokeActiveTokens = false });

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        string newSecret = body.GetProperty("clientSecret").GetString()!;
        newSecret.Should().NotBeNullOrEmpty().And.NotBe(oldSecret);
        body.GetProperty("client").GetProperty("clientId").GetString().Should().Be(clientId);
        body.GetProperty("issuer").GetString().Should().NotBeNullOrEmpty();
        body.GetProperty("apiBaseUrl").GetString().Should().NotBeNullOrEmpty();

        (await ClientCredentialsAsync(clientId, oldSecret)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await ClientCredentialsAsync(clientId, newSecret)).StatusCode.Should().Be(HttpStatusCode.OK);

        // The reveal is one-time: no read path carries the secret afterwards.
        HttpResponseMessage read = await Client.GetAsync($"/identity/organizations/{orgId}/clients/{clientId}");
        string readBody = await read.Content.ReadAsStringAsync();
        readBody.Should().NotContain(newSecret).And.NotContain("clientSecret");
    }

    [Fact]
    public async Task Rotate_WithoutTheFlag_LeavesAnApplicationsRefreshTokenWorking()
    {
        Guid orgId = await OrganizationOwnedBySomeoneElseAsync("Refresh Org");
        string managerEmail = await ActAsEnrolledEmailAsync(orgId, "manager");
        (string clientId, string oldSecret) = await RegisterApplicationAsync(orgId, "Dashboard");
        TokenOutcome tokens = await SignInThroughAsync(managerEmail, clientId, oldSecret);

        string newSecret = await RotateAsync(orgId, clientId, revokeActiveTokens: false);

        TokenOutcome refreshed = await Harness.RefreshAsync(clientId, newSecret, tokens.RefreshToken!);
        refreshed.StatusCode.Should().Be(HttpStatusCode.OK, refreshed.Body);
        refreshed.AccessToken.Should().NotBeNullOrEmpty();

        // The old secret is gone even for a refresh the grant would otherwise have allowed.
        TokenOutcome withOldSecret = await Harness.RefreshAsync(clientId, oldSecret, refreshed.RefreshToken!);
        withOldSecret.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        withOldSecret.Error.Should().Be("invalid_client");
    }

    [Fact]
    public async Task Rotate_WithTheFlag_EndsEveryTokenTheClientWasIssued()
    {
        Guid orgId = await OrganizationOwnedBySomeoneElseAsync("Revoke Org");
        string managerEmail = await ActAsEnrolledEmailAsync(orgId, "manager");
        (string clientId, string oldSecret) = await RegisterApplicationAsync(orgId, "Dashboard");
        TokenOutcome tokens = await SignInThroughAsync(managerEmail, clientId, oldSecret);

        string newSecret = await RotateAsync(orgId, clientId, revokeActiveTokens: true);

        TokenOutcome refreshed = await Harness.RefreshAsync(clientId, newSecret, tokens.RefreshToken!);
        refreshed.StatusCode.Should().Be(HttpStatusCode.BadRequest, refreshed.Body);
        refreshed.Error.Should().Be("invalid_grant");
    }

    [Fact]
    public async Task Rotate_WithTheFlag_RejectsAServiceAccountsOutstandingBearerToken()
    {
        Guid orgId = await OrganizationOwnedBySomeoneElseAsync("Bearer Org");
        await ActAsEnrolledAsync(orgId, "manager");
        (string clientId, string oldSecret) = await RegisterServiceAccountAsync(orgId, "Nightly sync");
        HttpResponseMessage minted = await ClientCredentialsAsync(clientId, oldSecret);
        string accessToken = (await minted.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("access_token").GetString()!;
        (await BearerCallAsync(accessToken)).StatusCode.Should().Be(HttpStatusCode.OK, "the token is good until it is revoked");

        await RotateAsync(orgId, clientId, revokeActiveTokens: true);

        (await BearerCallAsync(accessToken)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Rotate_WithoutTheFlag_LeavesAServiceAccountsOutstandingBearerTokenWorking()
    {
        Guid orgId = await OrganizationOwnedBySomeoneElseAsync("Keep Bearer Org");
        await ActAsEnrolledAsync(orgId, "manager");
        (string clientId, string oldSecret) = await RegisterServiceAccountAsync(orgId, "Nightly sync");
        HttpResponseMessage minted = await ClientCredentialsAsync(clientId, oldSecret);
        string accessToken = (await minted.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("access_token").GetString()!;

        await RotateAsync(orgId, clientId, revokeActiveTokens: false);

        (await BearerCallAsync(accessToken)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Rotate_RecordsWhoRotatedAndWhen_OnTheClient()
    {
        Guid orgId = await OrganizationOwnedBySomeoneElseAsync("Provenance Org");
        Guid actor = await ActAsEnrolledAsync(orgId, "admin");
        (string clientId, _) = await RegisterServiceAccountAsync(orgId, "Nightly sync");

        JsonElement before = await GetClientAsync(orgId, clientId);
        before.GetProperty("createdByUserId").GetGuid().Should().Be(actor);
        before.GetProperty("lastRotatedByUserId").ValueKind.Should().Be(JsonValueKind.Null);
        before.GetProperty("lastRotatedAt").ValueKind.Should().Be(JsonValueKind.Null);

        await RotateAsync(orgId, clientId, revokeActiveTokens: false);

        JsonElement after = await GetClientAsync(orgId, clientId);
        after.GetProperty("lastRotatedByUserId").GetGuid().Should().Be(actor);
        after.GetProperty("lastRotatedAt").GetDateTimeOffset().Should().BeAfter(before.GetProperty("createdAt").GetDateTimeOffset().AddSeconds(-1));
    }

    [Fact]
    public async Task RegisterAndRotate_AreAudited_WithActorOrganizationAndClient()
    {
        Guid orgId = await OrganizationOwnedBySomeoneElseAsync("Audit Org");
        Guid actor = await ActAsEnrolledAsync(orgId, "manager");
        (string clientId, _) = await RegisterServiceAccountAsync(orgId, "Nightly sync");
        await RotateAsync(orgId, clientId, revokeActiveTokens: true);

        AuthAuditEntry registered = await AuditRowAsync("ClientRegistered", clientId);
        registered.ActorId.Should().Be(actor);
        registered.UserId.Should().Be(actor);
        registered.TenantId.Should().Be(orgId);

        AuthAuditEntry rotated = await AuditRowAsync("ClientSecretRotated", clientId);
        rotated.ActorId.Should().Be(actor);
        rotated.TenantId.Should().Be(orgId);
    }

    [Fact]
    public async Task Rotate_AnswersNotFound_ForAnotherOrganizationsClient_AndForbidsAMember()
    {
        Guid ownerOrg = await OrganizationOwnedBySomeoneElseAsync("Rotation Owner Org");
        await ActAsEnrolledAsync(ownerOrg, "manager");
        (string clientId, _) = await RegisterServiceAccountAsync(ownerOrg, "Nightly sync");

        Guid otherOrg = await OrganizationOwnedBySomeoneElseAsync("Rotation Other Org");
        await ActAsEnrolledAsync(otherOrg, "manager");
        HttpResponseMessage crossOrg = await Client.PostAsJsonAsync(
            $"/identity/organizations/{otherOrg}/clients/{clientId}/rotate-secret",
            new { revokeActiveTokens = false });
        crossOrg.StatusCode.Should().Be(HttpStatusCode.NotFound);

        await ActAsEnrolledAsync(ownerOrg, "user");
        HttpResponseMessage asMember = await Client.PostAsJsonAsync(
            $"/identity/organizations/{ownerOrg}/clients/{clientId}/rotate-secret",
            new { revokeActiveTokens = false });
        asMember.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private AuthorizationCodeFlowHarness? _harness;

    private AuthorizationCodeFlowHarness Harness => _harness ??= new AuthorizationCodeFlowHarness(Factory);

    public override async Task DisposeAsync()
    {
        _harness?.Dispose();
        await base.DisposeAsync();
    }

    /// <summary>Signs the person in through the application and hands back its tokens, refresh token included.</summary>
    private async Task<TokenOutcome> SignInThroughAsync(string email, string clientId, string clientSecret)
    {
        await Harness.SignInAsync(email, MemberPassword);
        AuthorizeOutcome consent = await Harness.AuthorizeAsync(clientId, "openid offline_access");
        AuthorizeOutcome granted = await Harness.ConsentAsync(consent, grant: true);
        TokenOutcome tokens = await Harness.ExchangeCodeAsync(clientId, clientSecret, granted.Code!, granted.CodeVerifier);
        tokens.StatusCode.Should().Be(HttpStatusCode.OK, tokens.Body);
        tokens.RefreshToken.Should().NotBeNullOrEmpty();
        return tokens;
    }

    private async Task<string> RotateAsync(Guid orgId, string clientId, bool revokeActiveTokens)
    {
        HttpResponseMessage response = await Client.PostAsJsonAsync(
            $"/identity/organizations/{orgId}/clients/{clientId}/rotate-secret",
            new { revokeActiveTokens });
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("clientSecret").GetString()!;
    }

    private async Task<JsonElement> GetClientAsync(Guid orgId, string clientId)
    {
        HttpResponseMessage response = await Client.GetAsync($"/identity/organizations/{orgId}/clients/{clientId}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    /// <summary>
    /// Presents the bearer token to userinfo, the one endpoint the test host still validates
    /// through OpenIddict itself (the stub scheme would accept any bearer string).
    /// </summary>
    private async Task<HttpResponseMessage> BearerCallAsync(string accessToken)
    {
        using HttpClient bearerClient = Factory.CreateClient();
        bearerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await bearerClient.GetAsync("/connect/userinfo");
    }

    /// <summary>Waits for the audit handler, which runs off the request, to land the row.</summary>
    private async Task<AuthAuditEntry> AuditRowAsync(string eventType, string clientId)
    {
        IDbContextFactory<AuthAuditDbContext> contexts =
            Factory.Services.GetRequiredService<IDbContextFactory<AuthAuditDbContext>>();
        AuthAuditEntry? row = null;
        await WaitForAsync(async () =>
        {
            await using AuthAuditDbContext context = await contexts.CreateDbContextAsync();
            row = await context.AuthAuditEntries
                .FirstOrDefaultAsync(e => e.EventType == eventType && e.ClientId == clientId);
            return row is not null;
        });
        return row ?? throw new InvalidOperationException($"No '{eventType}' audit row for '{clientId}' arrived.");
    }

    private static async Task WaitForAsync(Func<Task<bool>> condition)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow.AddSeconds(10);
        while (!await condition())
        {
            if (DateTimeOffset.UtcNow > deadline)
            {
                return;
            }

            await Task.Delay(100);
        }
    }

    private Task<(string ClientId, string Secret)> RegisterApplicationAsync(Guid orgId, string name) =>
        RegisterAsync(orgId, new
        {
            kind = "application",
            name,
            redirectUris = new[] { AuthorizationCodeFlowHarness.RedirectUri },
            postLogoutRedirectUris = Array.Empty<string>(),
            scopes = _applicationScopes,
        });

    private Task<(string ClientId, string Secret)> RegisterServiceAccountAsync(Guid orgId, string name) =>
        RegisterAsync(orgId, new
        {
            kind = "service-account",
            name,
            redirectUris = Array.Empty<string>(),
            postLogoutRedirectUris = Array.Empty<string>(),
            scopes = _readScopes,
        });

    /// <summary>Registers a client and hands back the id and the once-shown secret.</summary>
    private async Task<(string ClientId, string Secret)> RegisterAsync(Guid orgId, object body)
    {
        HttpResponseMessage response = await Client.PostAsJsonAsync($"/identity/organizations/{orgId}/clients", body);
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        JsonElement result = await response.Content.ReadFromJsonAsync<JsonElement>();
        return (
            result.GetProperty("client").GetProperty("clientId").GetString()!,
            result.GetProperty("clientSecret").GetString()!);
    }

    private async Task<HttpResponseMessage> ClientCredentialsAsync(string clientId, string clientSecret)
    {
        using HttpClient tokenClient = Factory.CreateClient();
        tokenClient.DefaultRequestHeaders.Remove("Authorization");
        using FormUrlEncodedContent form = new(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
            ["scope"] = string.Join(' ', _readScopes),
        });

        return await tokenClient.PostAsync("/connect/token", form);
    }

    private async Task<Guid> OrganizationOwnedBySomeoneElseAsync(string name)
    {
        Guid owner = await AuthorizationCodeFlowHarness.CreateUserAsync(
            ScopedServices, $"owner-{Guid.NewGuid():N}@wallow.dev", "Owner1234!");
        return await AuthorizationCodeFlowHarness.CreateOrganizationAsync(ScopedServices, name, owner);
    }

    private async Task<Guid> ActAsEnrolledAsync(Guid orgId, string roleName)
    {
        (Guid userId, _) = await EnrollAndActAsync(orgId, roleName);
        return userId;
    }

    private async Task<string> ActAsEnrolledEmailAsync(Guid orgId, string roleName)
    {
        (_, string email) = await EnrollAndActAsync(orgId, roleName);
        return email;
    }

    /// <summary>
    /// Enrolls a fresh user under <paramref name="roleName"/> and makes the test client act as
    /// them; the email lets a test also sign that person in through the real login.
    /// </summary>
    private async Task<(Guid UserId, string Email)> EnrollAndActAsync(Guid orgId, string roleName)
    {
        string email = $"member-{Guid.NewGuid():N}@wallow.dev";
        Guid userId = await AuthorizationCodeFlowHarness.CreateUserAsync(ScopedServices, email, MemberPassword);
        await AuthorizationCodeFlowHarness.EnrollMemberAsync(ScopedServices, orgId, userId, roleName);
        SetTestUser(userId.ToString(), roleName);
        SetTestTenant(orgId);
        return (userId, email);
    }
}
