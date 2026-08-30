using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Wallow.Identity.IntegrationTests.OAuth2;
using Wallow.Tests.Common.Factories;

namespace Wallow.Identity.IntegrationTests.OrganizationClients;

/// <summary>
/// The org-scoped client surface: an organization admin or manager registers a developer
/// application and receives everything needed to run it. Backend-dependent because the surface
/// writes an OpenIddict application and a registered-client row in one request and the
/// permission check crosses the tenant query filter.
/// </summary>
[Trait("Category", "Integration")]
public class ApplicationRegistrationTests(WallowApiFactory factory) : IdentityIntegrationTestBase(factory)
{
    private const string RedirectUri = AuthorizationCodeFlowHarness.RedirectUri;
    private const string MemberPassword = "Member1234!";
    private static readonly string[] _portalRedirects = ["https://portal.example.com/callback"];
    private static readonly string[] _portalPostLogouts = ["https://portal.example.com/"];
    private static readonly string[] _portalScopes = ["openid", "email"];


    [Fact]
    public async Task Manager_RegistersAnApplication_AndReceivesItsDerivedIdAndOneTimeSecret()
    {
        Guid orgId = await OrganizationOwnedBySomeoneElseAsync("Acme Rockets");
        await ActAsEnrolledAsync(orgId, "manager");

        HttpResponseMessage response = await Client.PostAsJsonAsync(
            $"/identity/organizations/{orgId}/clients",
            RegisterBody("Dashboard", [RedirectUri], ["openid", "profile"]));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("client").GetProperty("clientId").GetString()
            .Should().Be("app-acme-rockets-dashboard");
        body.GetProperty("client").GetProperty("kind").GetString().Should().Be("application");
        body.GetProperty("client").GetProperty("status").GetString().Should().Be("active");
        body.GetProperty("clientSecret").GetString().Should().NotBeNullOrWhiteSpace();
        body.GetProperty("issuer").GetString().Should().NotBeNullOrWhiteSpace();
        body.GetProperty("apiBaseUrl").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Member_CannotRegisterAnApplication()
    {
        Guid orgId = await OrganizationOwnedBySomeoneElseAsync("Member Org");
        await ActAsEnrolledAsync(orgId, "user");

        HttpResponseMessage response = await Client.PostAsJsonAsync(
            $"/identity/organizations/{orgId}/clients",
            RegisterBody("Dashboard", [RedirectUri], ["openid"]));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ManagerOfAnotherOrganization_FindsNothingToManage()
    {
        Guid target = await OrganizationOwnedBySomeoneElseAsync("Target Org");
        Guid other = await OrganizationOwnedBySomeoneElseAsync("Other Org");
        await ActAsEnrolledAsync(other, "manager");

        HttpResponseMessage register = await Client.PostAsJsonAsync(
            $"/identity/organizations/{target}/clients",
            RegisterBody("Dashboard", [RedirectUri], ["openid"]));
        HttpResponseMessage list = await Client.GetAsync($"/identity/organizations/{target}/clients");

        register.StatusCode.Should().Be(HttpStatusCode.NotFound);
        list.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Ledger_ListsGetsPatchesAndDeletes_WithoutEverRevealingTheSecretAgain()
    {
        Guid orgId = await OrganizationOwnedBySomeoneElseAsync("Ledger Org");
        await ActAsEnrolledAsync(orgId, "admin");
        string clientId = await RegisterAsync(orgId, "Portal", ["openid", "profile"]);
        string baseUrl = $"/identity/organizations/{orgId}/clients";

        JsonElement listed = await Client.GetFromJsonAsync<JsonElement>(baseUrl);
        listed.EnumerateArray().Select(c => c.GetProperty("clientId").GetString())
            .Should().Contain(clientId);

        JsonElement fetched = await Client.GetFromJsonAsync<JsonElement>($"{baseUrl}/{clientId}");
        fetched.GetProperty("name").GetString().Should().Be("Portal");
        fetched.TryGetProperty("clientSecret", out _).Should().BeFalse();

        HttpResponseMessage patched = await Client.PatchAsJsonAsync(
            $"{baseUrl}/{clientId}",
            new
            {
                name = "Renamed",
                clientId = "app-something-else",
                redirectUris = _portalRedirects,
                postLogoutRedirectUris = _portalPostLogouts,
                scopes = _portalScopes,
            });
        patched.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonElement updated = await patched.Content.ReadFromJsonAsync<JsonElement>();
        updated.GetProperty("name").GetString().Should().Be("Portal");
        updated.GetProperty("clientId").GetString().Should().Be(clientId);
        updated.GetProperty("redirectUris").EnumerateArray().Select(u => u.GetString())
            .Should().Equal("https://portal.example.com/callback");
        updated.GetProperty("scopes").EnumerateArray().Select(u => u.GetString())
            .Should().BeEquivalentTo("openid", "email");

        HttpResponseMessage deleted = await Client.DeleteAsync($"{baseUrl}/{clientId}");
        deleted.StatusCode.Should().Be(HttpStatusCode.NoContent);
        HttpResponseMessage gone = await Client.GetAsync($"{baseUrl}/{clientId}");
        gone.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("", "https://app.example.com/cb", "openid", "name")]
    [InlineData("No Redirects", null, "openid", "redirectUris")]
    [InlineData("No Scopes", "https://app.example.com/cb", null, "scopes")]
    [InlineData("Fragment", "https://app.example.com/cb#frag", "openid", "redirectUris")]
    [InlineData("Relative", "/callback", "openid", "redirectUris")]
    [InlineData("Plain Http", "http://app.example.com/cb", "openid", "redirectUris")]
    public async Task Register_RefusesAnIncompleteOrUnsafeRequest(
        string name, string? redirectUri, string? scope, string offendingField)
    {
        Guid orgId = await OrganizationOwnedBySomeoneElseAsync($"Validation Org {Guid.NewGuid():N}");
        await ActAsEnrolledAsync(orgId, "manager");

        HttpResponseMessage response = await Client.PostAsJsonAsync(
            $"/identity/organizations/{orgId}/clients",
            RegisterBody(name, redirectUri is null ? [] : [redirectUri], scope is null ? [] : [scope]));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        JsonElement problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("errors").EnumerateObject().Select(e => e.Name)
            .Should().Contain(n => string.Equals(n, offendingField, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Register_AcceptsLoopbackHttp_ForLocalDevelopment()
    {
        Guid orgId = await OrganizationOwnedBySomeoneElseAsync("Loopback Org");
        await ActAsEnrolledAsync(orgId, "manager");

        HttpResponseMessage response = await Client.PostAsJsonAsync(
            $"/identity/organizations/{orgId}/clients",
            RegisterBody("Local", ["http://localhost:3000/oidc/callback"], ["openid"]));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Register_RefusesAPlatformOnlyScope_ByName()
    {
        Guid orgId = await OrganizationOwnedBySomeoneElseAsync("Scope Org");
        await ActAsEnrolledAsync(orgId, "manager");

        HttpResponseMessage response = await Client.PostAsJsonAsync(
            $"/identity/organizations/{orgId}/clients",
            RegisterBody("Greedy", [RedirectUri], ["openid", "users.manage"]));

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        JsonElement problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("code").GetString().Should().Be("Identity.PlatformOnlyScope");
        problem.GetProperty("detail").GetString().Should().Contain("users.manage");
    }

    [Fact]
    public async Task Register_RefusesAServiceAccount_UntilThatSurfaceExists()
    {
        Guid orgId = await OrganizationOwnedBySomeoneElseAsync("Kind Org");
        await ActAsEnrolledAsync(orgId, "manager");

        HttpResponseMessage response = await Client.PostAsJsonAsync(
            $"/identity/organizations/{orgId}/clients",
            RegisterBody("Robot", [RedirectUri], ["openid"], kind: "service-account"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task DerivedClientId_IsStable_AndASecondRegistrationOfTheSameNameIsRefused()
    {
        Guid orgId = await OrganizationOwnedBySomeoneElseAsync("Stable Org");
        await ActAsEnrolledAsync(orgId, "manager");

        string first = await RegisterAsync(orgId, "Mobile  App!", ["openid"]);
        HttpResponseMessage again = await Client.PostAsJsonAsync(
            $"/identity/organizations/{orgId}/clients",
            RegisterBody("mobile app", [RedirectUri], ["openid"]));

        first.Should().Be("app-stable-org-mobile-app");
        again.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        JsonElement problem = await again.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("code").GetString().Should().Be("Identity.ClientIdTaken");
    }

    /// <summary>
    /// The point of the whole surface: what a manager registers is a client a person can sign in
    /// through — consent, code, tokens carrying the organization the client is bound to.
    /// </summary>
    [Fact]
    public async Task RegisteredApplication_CompletesAFullLogin_ScopedToItsOrganization()
    {
        Guid orgId = await OrganizationOwnedBySomeoneElseAsync("Login Org");
        string managerEmail = await ActAsEnrolledAsync(orgId, "manager");

        HttpResponseMessage response = await Client.PostAsJsonAsync(
            $"/identity/organizations/{orgId}/clients",
            RegisterBody("Dashboard", [RedirectUri], ["openid", "profile", "offline_access"]));
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        string clientId = body.GetProperty("client").GetProperty("clientId").GetString()!;
        string clientSecret = body.GetProperty("clientSecret").GetString()!;

        using AuthorizationCodeFlowHarness harness = new(Factory);
        await harness.SignInAsync(managerEmail, MemberPassword);
        AuthorizeOutcome consent = await harness.AuthorizeAsync(clientId, "openid profile offline_access");
        consent.ReturnUrl.Should().NotBeNull("an organization-registered application asks for consent");
        AuthorizeOutcome granted = await harness.ConsentAsync(consent, grant: true);
        granted.Code.Should().NotBeNull();
        TokenOutcome tokens = await harness.ExchangeCodeAsync(clientId, clientSecret, granted.Code!, granted.CodeVerifier);

        tokens.StatusCode.Should().Be(HttpStatusCode.OK);
        AuthorizationCodeFlowHarness.ReadClaimValues(tokens.RequireAccessToken(), "org_id")
            .Should().Equal(orgId.ToString());
    }

    private async Task<string> RegisterAsync(Guid orgId, string name, string[] scopes)
    {
        HttpResponseMessage response = await Client.PostAsJsonAsync(
            $"/identity/organizations/{orgId}/clients",
            RegisterBody(name, [RedirectUri], scopes));
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("client").GetProperty("clientId").GetString()!;
    }

    private static object RegisterBody(
        string name,
        string[] redirectUris,
        string[] scopes,
        string kind = "application") =>
        new
        {
            kind,
            name,
            redirectUris,
            postLogoutRedirectUris = Array.Empty<string>(),
            scopes,
        };

    /// <summary>
    /// Creates an organization owned by a throwaway user so the test's own caller can be enrolled
    /// under any role — the creator is always an admin.
    /// </summary>
    private async Task<Guid> OrganizationOwnedBySomeoneElseAsync(string name)
    {
        Guid owner = await AuthorizationCodeFlowHarness.CreateUserAsync(
            ScopedServices, $"owner-{Guid.NewGuid():N}@wallow.dev", "Owner1234!");
        return await AuthorizationCodeFlowHarness.CreateOrganizationAsync(ScopedServices, name, owner);
    }

    /// <summary>
    /// Enrolls a fresh user under <paramref name="roleName"/> and makes the test client act as
    /// them; returns the email so a test can also sign that person in through the real login.
    /// </summary>
    private async Task<string> ActAsEnrolledAsync(Guid orgId, string roleName)
    {
        string email = $"member-{Guid.NewGuid():N}@wallow.dev";
        Guid userId = await AuthorizationCodeFlowHarness.CreateUserAsync(ScopedServices, email, MemberPassword);
        await AuthorizationCodeFlowHarness.EnrollMemberAsync(ScopedServices, orgId, userId, roleName);
        SetTestUser(userId.ToString(), roleName);
        SetTestTenant(orgId);
        return email;
    }
}
