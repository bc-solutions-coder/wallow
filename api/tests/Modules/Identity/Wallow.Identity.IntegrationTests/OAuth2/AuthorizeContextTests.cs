using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using Wallow.Tests.Common.Factories;

namespace Wallow.Identity.IntegrationTests.OAuth2;

/// <summary>
/// The authorize-context read behind the auth host's branded transaction screens. The returnUrl
/// is the credential: a caller presenting the pending authorize request — whose redirect_uri
/// exactly matches one the client registered — gets the client described; everything else is the
/// same shapeless 404, so the endpoint cannot be used to enumerate clients or read branding by
/// client id the way the removed anonymous endpoints could.
/// </summary>
public sealed class AuthorizeContextTests(WallowApiFactory factory) : IdentityIntegrationTestBase(factory)
{
    private const string ClientSecret = "authorize-context-secret";
    private const string ScopeName = "context.read";
    private const string ScopeDescription = "Read authorize-context things";

    private static readonly string[] _clientScopes = ["openid", "profile", ScopeName];

    [Fact]
    public async Task BoundClient_DescribesClientOrganizationAndScopes()
    {
        string clientId = "ctx-bound-client";
        Guid ownerId = await AuthorizationCodeFlowHarness.CreateUserAsync(
            ScopedServices, "ctx-owner@example.com", "CtxOwner1234!");
        Guid orgId = await AuthorizationCodeFlowHarness.CreateOrganizationAsync(
            ScopedServices, "Authorize Context Org", ownerId);
        await AuthorizationCodeFlowHarness.RegisterClientAsync(
            ScopedServices, clientId, ClientSecret, orgId, _clientScopes);
        await EnsureScopeAsync();

        HttpResponseMessage response = await GetContextAsync(
            ReturnUrl(clientId, scope: $"openid {ScopeName}"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonElement context = await response.Content.ReadFromJsonAsync<JsonElement>();
        context.GetProperty("clientId").GetString().Should().Be(clientId);
        context.GetProperty("displayName").GetString().Should().Be(clientId);
        context.GetProperty("organizationName").GetString().Should().Be("Authorize Context Org");
        context.GetProperty("firstParty").GetBoolean().Should().BeFalse();

        Dictionary<string, string?> scopes = context.GetProperty("scopes").EnumerateArray()
            .ToDictionary(
                s => s.GetProperty("name").GetString()!,
                s => s.GetProperty("description").GetString());
        scopes.Keys.Should().BeEquivalentTo("openid", ScopeName);
        scopes[ScopeName].Should().Be(ScopeDescription);
    }

    [Fact]
    public async Task ExplicitScopeParameter_NarrowsTheDescribedScopes()
    {
        string clientId = "ctx-narrowed-client";
        await AuthorizationCodeFlowHarness.RegisterClientAsync(
            ScopedServices, clientId, ClientSecret, null, _clientScopes, firstParty: true);
        await EnsureScopeAsync();

        HttpResponseMessage response = await GetContextAsync(
            ReturnUrl(clientId, scope: "openid profile"), scope: ScopeName);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonElement context = await response.Content.ReadFromJsonAsync<JsonElement>();
        context.GetProperty("scopes").EnumerateArray()
            .Select(s => s.GetProperty("name").GetString())
            .Should().BeEquivalentTo([ScopeName], "the consent redirect's granted set wins over the embedded scope");
    }

    [Fact]
    public async Task FirstPartyClient_IsMarkedFirstPartyWithNoOrganization()
    {
        string clientId = "ctx-first-party-client";
        await AuthorizationCodeFlowHarness.RegisterClientAsync(
            ScopedServices, clientId, ClientSecret, null, _clientScopes, firstParty: true);

        HttpResponseMessage response = await GetContextAsync(ReturnUrl(clientId));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonElement context = await response.Content.ReadFromJsonAsync<JsonElement>();
        context.GetProperty("firstParty").GetBoolean().Should().BeTrue();
        context.GetProperty("organizationName").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task RedirectUriTheClientNeverRegistered_IsNotFound()
    {
        string clientId = "ctx-redirect-probe-client";
        await AuthorizationCodeFlowHarness.RegisterClientAsync(
            ScopedServices, clientId, ClientSecret, null, _clientScopes, firstParty: true);

        HttpResponseMessage response = await GetContextAsync(
            ReturnUrl(clientId, redirectUri: "https://attacker.example.com/callback"));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RemovedAnonymousReadsByClientId_AnswerNotFound()
    {
        // The routes this endpoint replaced must stay gone: a resolvable client
        // whose context read succeeds is what makes the two refusals meaningful
        // rather than a typo'd URL 404ing on its own.
        string clientId = "ctx-legacy-read-client";
        await AuthorizationCodeFlowHarness.RegisterClientAsync(
            ScopedServices, clientId, ClientSecret, null, _clientScopes, firstParty: true);

        HttpResponseMessage context = await GetContextAsync(ReturnUrl(clientId));
        context.StatusCode.Should().Be(HttpStatusCode.OK);

        HttpResponseMessage branding = await Client.GetAsync(
            new Uri($"/identity/apps/{clientId}/branding", UriKind.Relative));
        HttpResponseMessage consentInfo = await Client.GetAsync(
            new Uri($"/identity/apps/consent-info/{clientId}", UriKind.Relative));

        branding.StatusCode.Should().Be(HttpStatusCode.NotFound);
        consentInfo.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UnknownClient_IsNotFound()
    {
        HttpResponseMessage response = await GetContextAsync(ReturnUrl("ctx-no-such-client"));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("/identity/users/me?client_id=x&redirect_uri=https%3A%2F%2Fexample.com%2Fcb")]
    [InlineData("https://evil.example.com/connect/authorize?client_id=x&redirect_uri=https%3A%2F%2Fexample.com%2Fcb")]
    [InlineData("/connect/authorize")]
    public async Task ReturnUrlThatIsNotAPendingAuthorizeRequest_IsNotFound(string returnUrl)
    {
        HttpResponseMessage response = await GetContextAsync(returnUrl);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private static string ReturnUrl(
        string clientId,
        string redirectUri = AuthorizationCodeFlowHarness.RedirectUri,
        string scope = "openid profile")
        => "/connect/authorize?response_type=code" +
            $"&client_id={Uri.EscapeDataString(clientId)}" +
            $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
            $"&scope={Uri.EscapeDataString(scope)}";

    private async Task<HttpResponseMessage> GetContextAsync(string returnUrl, string? scope = null)
    {
        string url = $"/identity/auth/authorize-context?returnUrl={Uri.EscapeDataString(returnUrl)}";
        if (scope is not null)
        {
            url += $"&scope={Uri.EscapeDataString(scope)}";
        }

        return await Client.GetAsync(new Uri(url, UriKind.Relative));
    }

    private async Task EnsureScopeAsync()
    {
        IOpenIddictScopeManager scopes = ScopedServices.GetRequiredService<IOpenIddictScopeManager>();
        object? existing = await scopes.FindByNameAsync(ScopeName);
        if (existing is null)
        {
            await scopes.CreateAsync(new OpenIddictScopeDescriptor
            {
                Name = ScopeName,
                Description = ScopeDescription,
            });
        }
    }
}
