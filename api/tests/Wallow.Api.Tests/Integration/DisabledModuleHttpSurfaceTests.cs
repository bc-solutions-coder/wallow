using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.OpenApi;
using Wallow.Shared.Contracts.ApiKeys;
using Wallow.Tests.Common.Bases;
using Wallow.Tests.Common.Factories;
using Wallow.Tests.Common.Helpers;

namespace Wallow.Api.Tests.Integration;

/// <summary>
/// Checks route and OpenAPI removal for disabled modules, with enabled-module controls.
/// UseSetting supplies flags before module registration reads them.
/// </summary>
[Collection(nameof(ApiIntegrationTestCollection))]
[Trait("Category", "Integration")]
public sealed class DisabledModuleHttpSurfaceTests(WallowApiFactory factory)
    : WallowIntegrationTestBase(factory)
{
    private const string ApiKeysPath = "/identity/auth/keys";
    private const string StorageConfigPath = "/storage/config";

    [Fact]
    public async Task DisabledModule_HasNoRoute_ForAnAuthenticatedRequest()
    {
        // The default configuration disables ApiKeys.
        SetTestUser(TestConstants.AdminUserId.ToString(), "admin");

        HttpResponseMessage response = await Client.GetAsync(ApiKeysPath);

        response.StatusCode.Should().Be(
            HttpStatusCode.NotFound,
            "a disabled module's endpoint must not exist, rather than exist and fail when it is called");
    }

    [Fact]
    public async Task EnabledModule_StillAnswers_WhileAnotherModuleIsDisabled()
    {
        // An enabled Storage route is the positive control.
        SetTestUser(TestConstants.AdminUserId.ToString(), "admin");

        HttpResponseMessage response = await Client.GetAsync(StorageConfigPath);

        response.StatusCode.Should().Be(
            HttpStatusCode.OK,
            "Storage is enabled by default and must be unaffected by the gate");
    }

    [Fact]
    public async Task DisabledModule_IsAbsentFromTheGeneratedOpenApiDocument()
    {
        // Testing does not expose the development-only document route; resolve its provider directly.
        IOpenApiDocumentProvider documentProvider =
            ScopedServices.GetRequiredKeyedService<IOpenApiDocumentProvider>("v1");

        OpenApiDocument document = await documentProvider.GetOpenApiDocumentAsync(CancellationToken.None);

        IEnumerable<string> paths = document.Paths.Keys;

        // Require an enabled path so an empty document cannot satisfy the absence check.
        paths.Should().Contain(
            path => path.Contains("storage/config", StringComparison.Ordinal),
            "an enabled module's endpoints must still be documented");
        paths.Should().NotContain(
            path => path.Contains("identity/auth/keys", StringComparison.Ordinal),
            "a disabled module must not advertise endpoints a caller cannot reach");
    }

    [Fact]
    public async Task DisabledModule_HasNoRoute_EvenWhenNothingStandsInForItsServices()
    {
        // Remove the factory fake to ensure a missing service cannot hide behind successful controller activation.
        using WebApplicationFactory<Program> withoutTheFake = Factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.RemoveAll<IApiKeyService>()));

        using HttpClient client = CreateAdminClient(withoutTheFake);

        HttpResponseMessage response = await client.GetAsync(ApiKeysPath);

        response.StatusCode.Should().Be(
            HttpStatusCode.NotFound,
            "a disabled module's endpoint must not exist; failing to activate its controller is the bug");
    }

    [Fact]
    public async Task EnabledByConfiguration_TheSameModuleAnswersAsItDoesToday()
    {
        // Enable ApiKeys and use the seeded admin identity for the permission check.
        using WebApplicationFactory<Program> apiKeysEnabled = Factory.WithWebHostBuilder(builder =>
            builder.UseSetting("FeatureManagement:Modules.ApiKeys", "true"));

        using HttpClient client = CreateAdminClient(apiKeysEnabled);

        HttpResponseMessage response = await client.GetAsync(ApiKeysPath);

        response.StatusCode.Should().Be(
            HttpStatusCode.OK,
            "an enabled module must keep the behaviour it has today");
    }

    [Fact]
    public async Task DisablingASecondModule_AlsoRemovesItsRoute()
    {
        // Check another optional module to catch an ApiKeys-specific gate.
        using WebApplicationFactory<Program> storageDisabled = Factory.WithWebHostBuilder(builder =>
            builder.UseSetting("FeatureManagement:Modules.Storage", "false"));

        using HttpClient client = CreateAdminClient(storageDisabled);

        HttpResponseMessage response = await client.GetAsync(StorageConfigPath);

        response.StatusCode.Should().Be(
            HttpStatusCode.NotFound,
            "every optional module must lose its HTTP surface when it is switched off, not just ApiKeys");
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
