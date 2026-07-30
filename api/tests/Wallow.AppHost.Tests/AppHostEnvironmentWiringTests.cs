using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using AwesomeAssertions;

namespace Wallow.AppHost.Tests;

/// <summary>
/// Verifies the Aspire AppHost wires the required OIDC/BFF/API configuration onto the
/// wallow-web and wallow-auth Node resources (Wallow-xzha.1.1). Without these, the first
/// BFF request under 'pnpm backend' 500s because loadBffConfigFromEnv() throws on the
/// missing variables, and wallow-auth's passthrough proxy cannot resolve its upstream API.
///
/// Known-correct target values are the Aspire-local ports set in Wallow.AppHost/Program.cs.
/// They deliberately do NOT match docker/docker-compose.test.yml, which uses :5050 for both the
/// issuer and the metadata URL because the containerised origins differ. Do not "align" them.
///
/// The vars naming where the API listens assert the manifest placeholder
/// <c>{wallow-api.bindings.http.url}</c> rather than a literal, because Program.cs derives them
/// from the API resource's endpoint. That is the point: in Publish mode the value stays a
/// per-environment binding, and a regression to a hardcoded URL would show up here as a literal.
/// Locally the same reference resolves to http://localhost:5001 — the DCP proxy in front of
/// Wallow.Api, whose port its launchSettings applicationUrl pins.
/// </summary>
public sealed class AppHostEnvironmentWiringTests : IClassFixture<AppHostFixture>
{
    private const string WebResourceName = "wallow-web";
    private const string AuthResourceName = "wallow-auth";
    private const string ApiResourceName = "wallow-api";

    /// <summary>How an <c>EndpointReference</c> to the API's http binding renders in Publish mode.</summary>
    private const string ApiBinding = "{wallow-api.bindings.http.url}";

    private readonly AppHostFixture _fixture;

    public AppHostEnvironmentWiringTests(AppHostFixture fixture)
    {
        _fixture = fixture;
    }

    private async Task<Dictionary<string, string>> GetEnvironmentAsync(string resourceName)
    {
        IResourceWithEnvironment resource = _fixture.Builder.Resources
            .OfType<IResourceWithEnvironment>()
            .Single(r => r.Name == resourceName);

        // Publish-mode resolution turns literal env into literals and references into manifest
        // placeholders, so declared configuration can be asserted without starting any container.
        IExecutionConfigurationResult result = await ExecutionConfigurationBuilder
            .Create(resource)
            .WithEnvironmentVariablesConfig()
            .BuildAsync(new DistributedApplicationExecutionContext(
                new DistributedApplicationExecutionContextOptions(DistributedApplicationOperation.Publish)
                {
                    ServiceProvider = _fixture.App.Services,
                }));

        return result.EnvironmentVariables.ToDictionary();
    }

    [Fact]
    public async Task WallowWeb_SetsAllRequiredBffEnvironmentVariables()
    {
        Dictionary<string, string> env = await GetEnvironmentAsync(WebResourceName);

        // The dev issuer is the wallow-auth origin, not the API's: appsettings.Development.json
        // sets AuthUrl=http://localhost:3002 and OpenIddictIssuerResolver echoes it, so the client
        // must EXPECT :3002 while fetching discovery from the API directly. Assert both: either one
        // alone permits a mismatched pair that would break the real flow. The issuer stays a
        // literal because it must equal what the API derives from AuthUrl, not wherever the auth
        // app happens to listen.
        env.Should().ContainKey("OIDC_ISSUER").WhoseValue.Should().Be("http://localhost:3002");
        env.Should().ContainKey("OIDC_METADATA_URL").WhoseValue.Should()
            .Be($"{ApiBinding}/.well-known/openid-configuration");
        env.Should().ContainKey("OIDC_CLIENT_ID").WhoseValue.Should().Be("wallow-web-client");
        env.Should().ContainKey("OIDC_CLIENT_SECRET").WhoseValue.Should().Be("wallow-web-secret");
        env.Should().ContainKey("OIDC_REDIRECT_URI").WhoseValue.Should().Be("http://localhost:3000/bff/callback");
        env.Should().ContainKey("OIDC_POST_LOGOUT_REDIRECT_URI").WhoseValue.Should().Be("http://localhost:3000");
        env.Should().ContainKey("BFF_API_BASE_URL").WhoseValue.Should().Be(ApiBinding);
    }

    [Fact]
    public async Task WallowWeb_SetsASealedCookiePassword()
    {
        Dictionary<string, string> env = await GetEnvironmentAsync(WebResourceName);

        env.Should().ContainKey("COOKIE_PASSWORD");
        env["COOKIE_PASSWORD"].Should().NotBeNullOrWhiteSpace();
        env["COOKIE_PASSWORD"].Length.Should().BeGreaterThanOrEqualTo(32, "iron sealed cookies require a >= 32 char password");
    }

    [Fact]
    public async Task WallowWeb_ReferencesTheApi()
    {
        Dictionary<string, string> env = await GetEnvironmentAsync(WebResourceName);

        env.Keys.Should().Contain(
            key => key.StartsWith($"services__{ApiResourceName}", StringComparison.Ordinal),
            "WithReference(api) must inject wallow-api service discovery variables");
    }

    [Fact]
    public async Task WallowAuth_PointsInternalApiUrlAtTheApiBinding()
    {
        Dictionary<string, string> env = await GetEnvironmentAsync(AuthResourceName);

        // WithReference(api) alone is not enough — the Node host never reads the
        // services__wallow-api__* discovery vars it injects, so the passthrough proxy's upstream
        // has to be named outright.
        env.Should().ContainKey("WALLOW_API_INTERNAL_URL").WhoseValue.Should().Be(ApiBinding);
    }

    [Fact]
    public async Task WallowAuth_ReferencesTheApi()
    {
        Dictionary<string, string> env = await GetEnvironmentAsync(AuthResourceName);

        env.Keys.Should().Contain(
            key => key.StartsWith($"services__{ApiResourceName}", StringComparison.Ordinal),
            "WithReference(api) must inject wallow-api service discovery variables");
    }
}
