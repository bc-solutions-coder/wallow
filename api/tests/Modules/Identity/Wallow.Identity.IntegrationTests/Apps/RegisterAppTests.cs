using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using Wallow.Tests.Common.Factories;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Wallow.Identity.IntegrationTests.Apps;

// Backend-dependent integration tests for the golden-path self-service app registration
// flow (AppsController POST /v1/identity/apps/register). Requires the full API plus the
// Postgres/Redis containers brought up by WallowApiFactory.
[Trait("Category", "Integration")]
public class RegisterAppTests(WallowApiFactory factory) : IdentityIntegrationTestBase(factory)
{
    private const string RegisterEndpoint = "/v1/identity/apps/register";

    // The EXACT scope set docs/integrations/bff-pattern.md instructs integrators to request.
    private static readonly string[] _bffLoginScopes = ["openid", "profile", "email", "offline_access"];
    private static readonly string[] _developerScope = ["inquiries.read"];

    [Fact]
    public async Task Should_Register_Confidential_App_With_Bff_Login_Scopes()
    {
        RegisterAppRequest request = new(
            "app-bff-golden-path",
            _bffLoginScopes,
            RedirectUris: ["https://app.example.com/callback"],
            PostLogoutRedirectUris: ["https://app.example.com/"]);

        HttpResponseMessage response = await Client.PostAsJsonAsync(RegisterEndpoint, request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        AppRegistrationResponse? result = await response.Content.ReadFromJsonAsync<AppRegistrationResponse>();
        result.Should().NotBeNull();
        result!.ClientId.Should().NotBeNullOrWhiteSpace();
        // A confidential client returns its secret exactly once, at creation time.
        result.ClientSecret.Should().NotBeNullOrWhiteSpace();

        await using AsyncServiceScope scope = Factory.Services.CreateAsyncScope();
        IOpenIddictApplicationManager manager =
            scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();

        object? application = await manager.FindByClientIdAsync(result.ClientId);
        application.Should().NotBeNull();

        string? clientType = await manager.GetClientTypeAsync(application!);
        clientType.Should().Be(ClientTypes.Confidential);

        // A BFF login client must speak the authorization-code + refresh-token OIDC flow,
        // not merely client_credentials.
        (await manager.HasPermissionAsync(application!, Permissions.Endpoints.Authorization))
            .Should().BeTrue();
        (await manager.HasPermissionAsync(application!, Permissions.GrantTypes.RefreshToken))
            .Should().BeTrue();
        (await manager.HasPermissionAsync(application!, Permissions.Endpoints.EndSession))
            .Should().BeTrue();
    }

    [Fact]
    public async Task Should_Reject_Non_Https_Non_Localhost_Redirect_Uri()
    {
        // Uses an already-allowed developer scope so the ONLY reason to reject is the URI scheme.
        RegisterAppRequest request = new(
            "app-insecure-redirect",
            _developerScope,
            RedirectUris: ["http://app.example.com/callback"]);

        HttpResponseMessage response = await Client.PostAsJsonAsync(RegisterEndpoint, request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private sealed record RegisterAppRequest(
        string ClientName,
        IReadOnlyList<string> RequestedScopes,
        string? ClientType = null,
        IReadOnlyList<string>? RedirectUris = null,
        IReadOnlyList<string>? PostLogoutRedirectUris = null);

    private sealed record AppRegistrationResponse(
        string ClientId,
        string ClientSecret,
        string RegistrationAccessToken);
}
