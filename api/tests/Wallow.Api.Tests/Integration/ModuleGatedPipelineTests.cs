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
/// The two things in <c>Program.cs</c> that are registered per module but are not routes: the ApiKeys
/// module's authentication middleware, and the Notifications module's <c>retry-failed-emails</c>
/// recurring job. Both used to ask <see cref="Microsoft.FeatureManagement.IFeatureManager"/> for a
/// <c>"Modules.X"</c> string of their own; both now read the enabled set the host already computed.
/// These facts pin the externally observable behaviour so the swap is provably behaviour-preserving.
/// </summary>
/// <remarks>
/// <para>
/// Flags are arranged, never assumed. <see cref="WallowApiFactory"/> overrides no
/// <c>FeatureManagement:Modules.*</c> key, so the default host runs the shipped
/// <c>appsettings.json</c>: ApiKeys off, everything else on. Flipping one uses <c>UseSetting</c>
/// because the flags are read while services are still being registered.
/// </para>
/// <para>
/// The middleware is observed through the one behaviour only it has: it inspects
/// <c>X-Api-Key</c>. <c>WallowApiFactory</c>'s stand-in <c>IApiKeyService</c> rejects every key, so a
/// request carrying a bogus <c>X-Api-Key</c> gets 401 when the middleware is in the pipeline and is
/// answered normally when it is not. That distinguishes presence from absence without depending on a
/// DI failure or on the ApiKeys module's own routes, which are already gated separately.
/// </para>
/// </remarks>
[Collection(nameof(ApiIntegrationTestCollection))]
[Trait("Category", "Integration")]
public sealed class ModuleGatedPipelineTests(WallowApiFactory factory)
    : WallowIntegrationTestBase(factory)
{
    /// <summary>An enabled module's endpoint, so the response reports the pipeline and not routing.</summary>
    private const string StorageConfigPath = "/storage/config";

    private const string BogusApiKey = "wallow_not-a-real-key";

    [Fact]
    public async Task ApiKeyMiddleware_IsNotInThePipeline_WhenTheApiKeysModuleIsDisabled()
    {
        // ApiKeys ships disabled, so nothing may act on X-Api-Key at all: the header is inert and the
        // request authenticates as it would without it.
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
        // The other half, and the reason the test above is not vacuous: with the flag on, the same
        // request is intercepted and the bogus key is rejected before the route runs.
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
        // Notifications ships enabled, so the gate must take the registering branch. The unconditional
        // system-heartbeat job registered immediately above it in Program.cs is the control: it proves
        // this really is reading the host's recurring-job storage, so a missing retry-failed-emails
        // means the gate did not fire rather than that the query found nothing at all.
        using IStorageConnection connection = Factory.Services.GetRequiredService<JobStorage>().GetConnection();

        IEnumerable<string> recurringJobIds = connection.GetRecurringJobs().Select(job => job.Id);

        recurringJobIds.Should().Contain(
            "system-heartbeat",
            "the ungated job next to it must be here, otherwise this assertion proves nothing");
        recurringJobIds.Should().Contain(
            "retry-failed-emails",
            "Notifications is enabled by default, so its recurring job must be registered");
    }

    /// <summary>
    /// The same headers <see cref="WallowIntegrationTestBase"/> attaches to its own client, for a
    /// client that needs extra headers or a derived factory.
    /// </summary>
    private static HttpClient CreateAdminClient(WebApplicationFactory<Program> factory)
    {
        HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("Authorization", "Bearer test-token");
        client.DefaultRequestHeaders.Add("X-Test-User-Id", TestConstants.AdminUserId.ToString());
        client.DefaultRequestHeaders.Add("X-Test-Roles", "admin");
        return client;
    }
}
