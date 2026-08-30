using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Wallow.Identity.IntegrationTests.OAuth2;
using Wallow.Tests.Common.Factories;

namespace Wallow.Identity.IntegrationTests.OrganizationClients;

/// <summary>
/// Service accounts on the org-scoped client surface: the same route, permission and scope
/// ceiling as developer applications, but a client-credentials client whose tokens carry the
/// organization it was registered under. Backend-dependent because the proof is a real token
/// minted by OpenIddict. The test host authenticates through a stub scheme, so a real token
/// reaching a tenant-scoped endpoint is proven by the containerised e2e run instead.
/// </summary>
[Trait("Category", "Integration")]
public class ServiceAccountRegistrationTests(WallowApiFactory factory) : IdentityIntegrationTestBase(factory)
{
    private const string MemberPassword = "Member1234!";
    private static readonly string[] _malformedRedirects = ["not a uri"];
    private static readonly string[] _relativePostLogouts = ["/relative"];
    private static readonly string[] _readScopes = ["organizations.read"];
    private static readonly string[] _inquiryScopes = ["inquiries.read"];

    [Fact]
    public async Task RegisteredServiceAccount_MintsAClientCredentialsToken_CarryingItsOrganizationAndScopes()
    {
        Guid orgId = await OrganizationOwnedBySomeoneElseAsync("Robot Org");
        await ActAsEnrolledAsync(orgId, "manager");

        HttpResponseMessage response = await Client.PostAsJsonAsync(
            $"/identity/organizations/{orgId}/clients",
            RegisterBody("Nightly Sync", ["organizations.read"]));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        JsonElement client = body.GetProperty("client");
        client.GetProperty("clientId").GetString().Should().Be("sa-robot-org-nightly-sync");
        client.GetProperty("kind").GetString().Should().Be("service-account");
        client.GetProperty("redirectUris").GetArrayLength().Should().Be(0);
        string clientId = client.GetProperty("clientId").GetString()!;
        string clientSecret = body.GetProperty("clientSecret").GetString()!;

        string accessToken = await ClientCredentialsTokenAsync(clientId, clientSecret, "organizations.read");
        AuthorizationCodeFlowHarness.ReadClaimValues(accessToken, "org_id").Should().Equal(orgId.ToString());
        AuthorizationCodeFlowHarness.ReadClaimValues(accessToken, "scope").Should().Contain("organizations.read");
        AuthorizationCodeFlowHarness.ReadClaimValues(accessToken, "sub").Should().Equal(clientId);
    }

    [Fact]
    public async Task Register_RefusesAPlatformOnlyScope_ForAServiceAccount()
    {
        Guid orgId = await OrganizationOwnedBySomeoneElseAsync("Ceiling Org");
        await ActAsEnrolledAsync(orgId, "manager");

        HttpResponseMessage response = await Client.PostAsJsonAsync(
            $"/identity/organizations/{orgId}/clients",
            RegisterBody("Escalator", ["users.manage"]));

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        JsonElement problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("code").GetString().Should().Be("Identity.PlatformOnlyScope");
    }

    [Fact]
    public async Task Register_IgnoresEveryUriField_ForAServiceAccount()
    {
        Guid orgId = await OrganizationOwnedBySomeoneElseAsync("Uri Org");
        await ActAsEnrolledAsync(orgId, "manager");

        HttpResponseMessage response = await Client.PostAsJsonAsync(
            $"/identity/organizations/{orgId}/clients",
            new
            {
                kind = "service-account",
                name = "Importer",
                redirectUris = _malformedRedirects,
                postLogoutRedirectUris = _relativePostLogouts,
                backchannelLogoutUri = "nope",
                scopes = _readScopes,
            });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        JsonElement client = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("client");
        client.GetProperty("redirectUris").GetArrayLength().Should().Be(0);
        client.GetProperty("postLogoutRedirectUris").GetArrayLength().Should().Be(0);
        client.GetProperty("backchannelLogoutUri").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Patch_UpdatesScopesAndStillIgnoresEveryUriField_ForAServiceAccount()
    {
        Guid orgId = await OrganizationOwnedBySomeoneElseAsync("Patch Org");
        await ActAsEnrolledAsync(orgId, "manager");
        string baseUrl = $"/identity/organizations/{orgId}/clients";

        HttpResponseMessage registered = await Client.PostAsJsonAsync(baseUrl, RegisterBody("Reporter", _readScopes));
        registered.StatusCode.Should().Be(HttpStatusCode.Created);
        string clientId = (await registered.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("client").GetProperty("clientId").GetString()!;

        HttpResponseMessage patched = await Client.PatchAsJsonAsync(
            $"{baseUrl}/{clientId}",
            new
            {
                redirectUris = _malformedRedirects,
                postLogoutRedirectUris = _relativePostLogouts,
                backchannelLogoutUri = "nope",
                scopes = _inquiryScopes,
            });

        patched.StatusCode.Should().Be(HttpStatusCode.OK, await patched.Content.ReadAsStringAsync());
        JsonElement updated = await patched.Content.ReadFromJsonAsync<JsonElement>();
        updated.GetProperty("scopes").EnumerateArray().Select(u => u.GetString()).Should().Equal("inquiries.read");
        updated.GetProperty("redirectUris").GetArrayLength().Should().Be(0);
        updated.GetProperty("postLogoutRedirectUris").GetArrayLength().Should().Be(0);
        updated.GetProperty("backchannelLogoutUri").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Register_RequiresANameAndAScope_ForAServiceAccount()
    {
        Guid orgId = await OrganizationOwnedBySomeoneElseAsync("Blank Org");
        await ActAsEnrolledAsync(orgId, "manager");

        HttpResponseMessage response = await Client.PostAsJsonAsync(
            $"/identity/organizations/{orgId}/clients",
            RegisterBody("", []));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        JsonElement problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        List<string> fields = problem.GetProperty("errors").EnumerateObject().Select(e => e.Name.ToLowerInvariant()).ToList();
        fields.Should().Contain("name").And.Contain("scopes").And.NotContain("redirecturis");
    }

    private async Task<string> ClientCredentialsTokenAsync(string clientId, string clientSecret, string scope)
    {
        using HttpClient tokenClient = Factory.CreateClient();
        tokenClient.DefaultRequestHeaders.Remove("Authorization");
        using FormUrlEncodedContent form = new(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
            ["scope"] = scope,
        });

        HttpResponseMessage response = await tokenClient.PostAsync("/connect/token", form);
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("access_token").GetString()!;
    }

    private static object RegisterBody(string name, string[] scopes) =>
        new
        {
            kind = "service-account",
            name,
            redirectUris = Array.Empty<string>(),
            postLogoutRedirectUris = Array.Empty<string>(),
            scopes,
        };

    private async Task<Guid> OrganizationOwnedBySomeoneElseAsync(string name)
    {
        Guid owner = await AuthorizationCodeFlowHarness.CreateUserAsync(
            ScopedServices, $"owner-{Guid.NewGuid():N}@wallow.dev", "Owner1234!");
        return await AuthorizationCodeFlowHarness.CreateOrganizationAsync(ScopedServices, name, owner);
    }

    private async Task ActAsEnrolledAsync(Guid orgId, string roleName)
    {
        string email = $"member-{Guid.NewGuid():N}@wallow.dev";
        Guid userId = await AuthorizationCodeFlowHarness.CreateUserAsync(ScopedServices, email, MemberPassword);
        await AuthorizationCodeFlowHarness.EnrollMemberAsync(ScopedServices, orgId, userId, roleName);
        SetTestUser(userId.ToString(), roleName);
        SetTestTenant(orgId);
    }
}
