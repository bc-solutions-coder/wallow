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
/// A module that is switched off must have no HTTP surface: no route, and no entry in the OpenAPI
/// document. Today only its DI registrations, its Wolverine handlers and its migrations are gated,
/// so its controllers stay routed and fail at request time instead of not existing.
/// </summary>
/// <remarks>
/// <para>
/// The flags are arranged, never assumed. <see cref="WallowApiFactory"/> does not override
/// <c>FeatureManagement:Modules.*</c> at all, so the default host runs on what
/// <c>api/src/Wallow.Api/appsettings.json</c> ships: ApiKeys off, everything else on. Flipping one
/// uses <c>UseSetting</c> rather than <c>ConfigureAppConfiguration</c> — the flags are read while
/// services are still being registered, which is before <c>ConfigureAppConfiguration</c>'s sources
/// are in play.
/// </para>
/// <para>
/// Every request below authenticates. An anonymous request is rejected by the authentication
/// challenge before routing ever resolves an action, which is exactly why this gap went unnoticed.
/// </para>
/// </remarks>
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
        // ApiKeys ships disabled, so on the shipped default configuration this endpoint does not exist.
        SetTestUser(TestConstants.AdminUserId.ToString(), "admin");

        HttpResponseMessage response = await Client.GetAsync(ApiKeysPath);

        response.StatusCode.Should().Be(
            HttpStatusCode.NotFound,
            "a disabled module's endpoint must not exist, rather than exist and fail when it is called");
    }

    [Fact]
    public async Task EnabledModule_StillAnswers_WhileAnotherModuleIsDisabled()
    {
        // The control case: gating ApiKeys must not cost an enabled module its own routes.
        // GetConfig carries only the class-level [Authorize] — no permission or query-parameter
        // precondition — so a non-200 here can only mean the route itself went missing.
        SetTestUser(TestConstants.AdminUserId.ToString(), "admin");

        HttpResponseMessage response = await Client.GetAsync(StorageConfigPath);

        response.StatusCode.Should().Be(
            HttpStatusCode.OK,
            "Storage is enabled by default and must be unaffected by the gate");
    }

    [Fact]
    public async Task DisabledModule_IsAbsentFromTheGeneratedOpenApiDocument()
    {
        // The document is generated straight from DI rather than fetched over HTTP: app.MapOpenApi()
        // is Development-only, but AddOpenApi("v1", ...) registers this provider in every environment.
        IOpenApiDocumentProvider documentProvider =
            ScopedServices.GetRequiredKeyedService<IOpenApiDocumentProvider>("v1");

        OpenApiDocument document = await documentProvider.GetOpenApiDocumentAsync(CancellationToken.None);

        IEnumerable<string> paths = document.Paths.Keys;

        // The positive half comes first deliberately: it is what stops the negative one below from
        // passing on an empty or unbuilt document.
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
        // The same criterion under production's DI shape. WallowApiFactory registers a fake
        // IApiKeyService unconditionally, which is a service ApiKeysController needs and which a host
        // running with Modules.ApiKeys=false would not have; dropping it here is what makes the
        // controller unconstructible exactly as it is in a real fork that switched the module off.
        // The fix must make this 404 by routing, before anything is ever asked to construct.
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
        // The other half of the criterion: with the flag on, ApiKeys behaves exactly as it does now.
        // SetTestUser(..., "admin") is sufficient for [HasPermission(ApiKeyManage)] — PermissionExpansionMiddleware
        // expands the role claim into permission claims before authorization runs.
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
        // Storage, so the fix is not accidentally specific to ApiKeys.
        using WebApplicationFactory<Program> storageDisabled = Factory.WithWebHostBuilder(builder =>
            builder.UseSetting("FeatureManagement:Modules.Storage", "false"));

        using HttpClient client = CreateAdminClient(storageDisabled);

        HttpResponseMessage response = await client.GetAsync(StorageConfigPath);

        response.StatusCode.Should().Be(
            HttpStatusCode.NotFound,
            "every optional module must lose its HTTP surface when it is switched off, not just ApiKeys");
    }

    /// <summary>
    /// The same headers <see cref="WallowIntegrationTestBase"/> attaches to its own client, for a
    /// client built off a derived factory.
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
