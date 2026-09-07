using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using AwesomeAssertions;

namespace Wallow.AppHost.Tests;

/// <summary>
/// Checks declared BFF and auth-proxy configuration in the Aspire application model.
/// API URLs must retain endpoint bindings in publish output.
/// </summary>
public sealed class AppHostEnvironmentWiringTests : IClassFixture<AppHostFixture>
{
    private const string WebResourceName = "wallow-web";
    private const string AuthResourceName = "wallow-auth";
    private const string ApiResourceName = "wallow-api";

    /// <summary>
    /// API endpoint placeholder expected in publish output.
    /// </summary>
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

        // Inspect publish configuration without starting the resources.
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

        // The expected issuer follows AuthUrl, while discovery is fetched from the API binding.
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

        // The auth proxy reads this explicit upstream setting.
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
