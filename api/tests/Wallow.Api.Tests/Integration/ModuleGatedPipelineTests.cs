using System.Net;
using Hangfire;
using Hangfire.Storage;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Wallow.Tests.Common.Bases;
using Wallow.Tests.Common.Factories;
using Wallow.Tests.Common.Helpers;

namespace Wallow.Api.Tests.Integration;

/// <summary>
/// Checks module-gated API-key middleware and Notifications job registration.
/// UseSetting supplies flags before module registration reads them.
/// </summary>
[Collection(nameof(ApiIntegrationTestCollection))]
[Trait("Category", "Integration")]
public sealed class ModuleGatedPipelineTests(WallowApiFactory factory)
    : WallowIntegrationTestBase(factory)
{
    /// <summary>
    /// Enabled Storage route used to probe the middleware pipeline.
    /// </summary>
    private const string StorageConfigPath = "/storage/config";

    private const string BogusApiKey = "wallow_not-a-real-key";

    [Fact]
    public async Task ApiKeyMiddleware_IsNotInThePipeline_WhenTheApiKeysModuleIsDisabled()
    {
        // ApiKeys is disabled in the default configuration.
        using HttpClient client = CreateAdminClient(Factory);
        client.DefaultRequestHeaders.Add("X-Api-Key", BogusApiKey);

        HttpResponseMessage response = await client.GetAsync(StorageConfigPath);

        response.StatusCode.Should().Be(
            HttpStatusCode.OK,
            "with ApiKeys off, ApiKeyAuthenticationMiddleware must not be registered, so an X-Api-Key " +
            "header is never validated and the request falls through to JWT authentication");
    }

    [Fact]
    public async Task ApiKeyMiddleware_IsInThePipeline_WhenTheApiKeysModuleIsEnabled()
    {
        // With ApiKeys enabled, the factory service rejects this key.
        using WebApplicationFactory<Program> apiKeysEnabled = Factory.WithWebHostBuilder(builder =>
            builder.UseSetting("FeatureManagement:Modules.ApiKeys", "true"));

        using HttpClient client = CreateAdminClient(apiKeysEnabled);
        client.DefaultRequestHeaders.Add("X-Api-Key", BogusApiKey);

        HttpResponseMessage response = await client.GetAsync(StorageConfigPath);

        response.StatusCode.Should().Be(
            HttpStatusCode.Unauthorized,
            "with ApiKeys on, ApiKeyAuthenticationMiddleware must be registered and must reject a key " +
            "its IApiKeyService says is invalid");
    }

    [Fact]
    public void NotificationsRecurringJob_IsRegistered_WhenTheNotificationsModuleIsEnabled()
    {
        // The unconditional heartbeat job confirms this storage contains the host registrations.
        using IStorageConnection connection = Factory.Services.GetRequiredService<JobStorage>().GetConnection();

        IEnumerable<string> recurringJobIds = connection.GetRecurringJobs().Select(job => job.Id);

        recurringJobIds.Should().Contain(
            "system-heartbeat",
            "the ungated job next to it must be here, otherwise this assertion proves nothing");
        recurringJobIds.Should().Contain(
            "retry-failed-emails",
            "Notifications is enabled by default, so its recurring job must be registered");
    }

    private static HttpClient CreateAdminClient(WebApplicationFactory<Program> factory)
    {
        HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("Authorization", "Bearer test-token");
        client.DefaultRequestHeaders.Add("X-Test-User-Id", TestConstants.AdminUserId.ToString());
        client.DefaultRequestHeaders.Add("X-Test-Roles", "admin");
        return client;
    }
}
