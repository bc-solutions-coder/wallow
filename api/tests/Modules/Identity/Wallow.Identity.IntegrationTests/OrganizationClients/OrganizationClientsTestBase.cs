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
/// What every org-scoped client suite needs: an organization someone else owns, a member enrolled
/// under a role and acting as the caller, clients of both kinds registered through the API, the
/// real sign-in through the authorization-code harness, and the audit row a lifecycle event lands.
/// </summary>
public abstract class OrganizationClientsTestBase(WallowApiFactory factory) : IdentityIntegrationTestBase(factory)
{
    protected const string MemberPassword = "Member1234!";
    protected static readonly string[] ApplicationScopes = ["openid", "offline_access"];
    protected static readonly string[] ReadScopes = ["organizations.read"];

    private AuthorizationCodeFlowHarness? _harness;

    protected AuthorizationCodeFlowHarness Harness => _harness ??= new AuthorizationCodeFlowHarness(Factory);

    public override async Task DisposeAsync()
    {
        _harness?.Dispose();
        await base.DisposeAsync();
    }

    /// <summary>Signs the person in through the application and hands back its tokens, refresh token included.</summary>
    protected async Task<TokenOutcome> SignInThroughAsync(string email, string clientId, string clientSecret)
    {
        await Harness.SignInAsync(email, MemberPassword);
        AuthorizeOutcome consent = await Harness.AuthorizeAsync(clientId, "openid offline_access");
        AuthorizeOutcome granted = await Harness.ConsentAsync(consent, grant: true);
        TokenOutcome tokens = await Harness.ExchangeCodeAsync(clientId, clientSecret, granted.Code!, granted.CodeVerifier);
        tokens.StatusCode.Should().Be(HttpStatusCode.OK, tokens.Body);
        tokens.RefreshToken.Should().NotBeNullOrEmpty();
        return tokens;
    }

    protected async Task<string> RotateAsync(Guid orgId, string clientId, bool revokeActiveTokens)
    {
        HttpResponseMessage response = await Client.PostAsJsonAsync(
            $"/identity/organizations/{orgId}/clients/{clientId}/rotate-secret",
            new { revokeActiveTokens });
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("clientSecret").GetString()!;
    }

    protected async Task<JsonElement> GetClientAsync(Guid orgId, string clientId)
    {
        HttpResponseMessage response = await Client.GetAsync($"/identity/organizations/{orgId}/clients/{clientId}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    /// <summary>
    /// Presents the bearer token to userinfo, the one endpoint the test host still validates
    /// through OpenIddict itself (the stub scheme would accept any bearer string).
    /// </summary>
    protected async Task<HttpResponseMessage> BearerCallAsync(string accessToken)
    {
        using HttpClient bearerClient = Factory.CreateClient();
        bearerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await bearerClient.GetAsync("/connect/userinfo");
    }

    /// <summary>Waits for the audit handler, which runs off the request, to land the row.</summary>
    protected async Task<AuthAuditEntry> AuditRowAsync(string eventType, string clientId)
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

    protected static async Task WaitForAsync(Func<Task<bool>> condition)
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

    protected Task<(string ClientId, string Secret)> RegisterApplicationAsync(Guid orgId, string name) =>
        RegisterAsync(orgId, new
        {
            kind = "application",
            name,
            redirectUris = new[] { AuthorizationCodeFlowHarness.RedirectUri },
            postLogoutRedirectUris = Array.Empty<string>(),
            scopes = ApplicationScopes,
        });

    protected Task<(string ClientId, string Secret)> RegisterServiceAccountAsync(Guid orgId, string name) =>
        RegisterAsync(orgId, new
        {
            kind = "service-account",
            name,
            redirectUris = Array.Empty<string>(),
            postLogoutRedirectUris = Array.Empty<string>(),
            scopes = ReadScopes,
        });

    /// <summary>Registers a client and hands back the id and the once-shown secret.</summary>
    protected async Task<(string ClientId, string Secret)> RegisterAsync(Guid orgId, object body)
    {
        HttpResponseMessage response = await Client.PostAsJsonAsync($"/identity/organizations/{orgId}/clients", body);
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        JsonElement result = await response.Content.ReadFromJsonAsync<JsonElement>();
        return (
            result.GetProperty("client").GetProperty("clientId").GetString()!,
            result.GetProperty("clientSecret").GetString()!);
    }

    protected async Task<HttpResponseMessage> ClientCredentialsAsync(string clientId, string clientSecret)
    {
        using HttpClient tokenClient = Factory.CreateClient();
        tokenClient.DefaultRequestHeaders.Remove("Authorization");
        using FormUrlEncodedContent form = new(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
            ["scope"] = string.Join(' ', ReadScopes),
        });

        return await tokenClient.PostAsync("/connect/token", form);
    }

    protected async Task<Guid> OrganizationOwnedBySomeoneElseAsync(string name)
    {
        Guid owner = await AuthorizationCodeFlowHarness.CreateUserAsync(
            ScopedServices, $"owner-{Guid.NewGuid():N}@wallow.dev", "Owner1234!");
        return await AuthorizationCodeFlowHarness.CreateOrganizationAsync(ScopedServices, name, owner);
    }

    protected async Task<Guid> ActAsEnrolledAsync(Guid orgId, string roleName)
    {
        (Guid userId, _) = await EnrollAndActAsync(orgId, roleName);
        return userId;
    }

    protected async Task<string> ActAsEnrolledEmailAsync(Guid orgId, string roleName)
    {
        (_, string email) = await EnrollAndActAsync(orgId, roleName);
        return email;
    }

    /// <summary>
    /// Enrolls a fresh user under <paramref name="roleName"/> and makes the test client act as
    /// them; the email lets a test also sign that person in through the real login.
    /// </summary>
    protected async Task<(Guid UserId, string Email)> EnrollAndActAsync(Guid orgId, string roleName)
    {
        string email = $"member-{Guid.NewGuid():N}@wallow.dev";
        Guid userId = await AuthorizationCodeFlowHarness.CreateUserAsync(ScopedServices, email, MemberPassword);
        await AuthorizationCodeFlowHarness.EnrollMemberAsync(ScopedServices, orgId, userId, roleName);
        SetTestUser(userId.ToString(), roleName);
        SetTestTenant(orgId);
        return (userId, email);
    }
}
