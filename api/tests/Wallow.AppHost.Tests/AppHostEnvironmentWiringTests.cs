using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using AwesomeAssertions;

namespace Wallow.AppHost.Tests;

/// <summary>
/// Verifies the Aspire AppHost wires the required OIDC/BFF/API configuration onto the
/// wallow-web and wallow-auth Node resources (Wallow-xzha.1.1). Without these, the first
/// BFF request under 'pnpm backend' 500s because loadBffConfigFromEnv() throws on the
/// missing variables, and wallow-auth's h3 proxy cannot resolve its upstream API.
///
/// Known-correct target values come from the bead (Aspire-local ports) and mirror the
/// containerised values proven in docker/docker-compose.test.yml.
/// </summary>
public sealed class AppHostEnvironmentWiringTests : IClassFixture<AppHostFixture>
{
    private const string WebResourceName = "wallow-web";
    private const string AuthResourceName = "wallow-auth";
    private const string ApiResourceName = "wallow-api";

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

        env.Should().ContainKey("OIDC_ISSUER").WhoseValue.Should().Be("http://localhost:5001");
        env.Should().ContainKey("OIDC_CLIENT_ID").WhoseValue.Should().Be("wallow-web-client");
        env.Should().ContainKey("OIDC_CLIENT_SECRET").WhoseValue.Should().Be("wallow-web-secret");
        env.Should().ContainKey("OIDC_REDIRECT_URI").WhoseValue.Should().Be("http://localhost:3000/bff/callback");
        env.Should().ContainKey("OIDC_POST_LOGOUT_REDIRECT_URI").WhoseValue.Should().Be("http://localhost:3000");
        env.Should().ContainKey("BFF_API_BASE_URL").WhoseValue.Should().Be("http://localhost:5001");
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
    public async Task WallowAuth_SetsInternalApiUrlToTheLocalApi()
    {
        Dictionary<string, string> env = await GetEnvironmentAsync(AuthResourceName);

        env.Should().ContainKey("WALLOW_API_INTERNAL_URL").WhoseValue.Should().Be("http://localhost:5001");
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
