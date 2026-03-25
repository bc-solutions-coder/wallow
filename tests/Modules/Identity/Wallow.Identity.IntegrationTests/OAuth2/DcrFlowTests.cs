using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Wallow.Tests.Common.Factories;

namespace Wallow.Identity.IntegrationTests.OAuth2;

[Trait("Category", "Integration")]
public class DcrFlowTests(WallowApiFactory factory) : IdentityIntegrationTestBase(factory)
{
    private const string RegisterEndpoint = "/api/v1/identity/apps/register";
    private const string TokenEndpoint = "/connect/token";

    [Fact]
    public async Task Should_Register_App_And_Acquire_Token()
    {
        AppRegistrationResponse registration = await RegisterAppAsync(
            "app-test-dcr",
            ["inquiries.read", "storage.read"]);

        registration.ClientId.Should().NotBeNullOrWhiteSpace();
        registration.ClientSecret.Should().NotBeNullOrWhiteSpace();

        string? token = await AcquireTokenAsync(registration.ClientId, registration.ClientSecret);

        token.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Should_Verify_Scopes_In_Token()
    {
        AppRegistrationResponse registration = await RegisterAppAsync(
            "app-test-scopes",
            ["inquiries.read", "storage.read"]);

        string? token = await AcquireTokenAsync(
            registration.ClientId,
            registration.ClientSecret,
            ["inquiries.read", "storage.read"]);
        token.Should().NotBeNullOrWhiteSpace();

        JwtSecurityTokenHandler handler = new();
        JwtSecurityToken jwt = handler.ReadJwtToken(token!);

        List<string> scopes = jwt.Claims
            .Where(c => c.Type is "scope" or "scp")
            .SelectMany(c => c.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .ToList();

        scopes.Should().Contain("inquiries.read");
        scopes.Should().Contain("storage.read");
    }

    [Fact]
    public async Task Should_Reject_Client_Name_Without_App_Prefix()
    {
        HttpResponseMessage response = await Client.PostAsJsonAsync(
            RegisterEndpoint,
            new RegisterAppRequest("invalid-name", ["inquiries.read"]));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Should_Reject_Invalid_Scopes()
    {
        HttpResponseMessage response = await Client.PostAsJsonAsync(
            RegisterEndpoint,
            new RegisterAppRequest("app-test-invalid-scopes", ["admin.all"]));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Should_Reject_Token_Request_With_Wrong_Credentials()
    {
        AppRegistrationResponse registration = await RegisterAppAsync(
            "app-test-wrong-creds",
            ["inquiries.read"]);

        HttpResponseMessage response = await PostTokenRequestAsync(
            registration.ClientId,
            "completely-wrong-secret");

        response.StatusCode.Should().NotBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Dcr_Client_Token_Should_Not_Have_Service_Account_Scopes()
    {
        AppRegistrationResponse registration = await RegisterAppAsync(
            "app-test-no-sa-access",
            ["inquiries.read"]);

        string? token = await AcquireTokenAsync(
            registration.ClientId,
            registration.ClientSecret,
            ["inquiries.read"]);
        token.Should().NotBeNullOrWhiteSpace();

        JwtSecurityTokenHandler handler = new();
        JwtSecurityToken jwt = handler.ReadJwtToken(token!);

        List<string> scopes = jwt.Claims
            .Where(c => c.Type is "scope" or "scp")
            .SelectMany(c => c.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .ToList();

        List<string> roles = jwt.Claims
            .Where(c => c.Type is "role")
            .Select(c => c.Value)
            .ToList();

        scopes.Should().NotContain("serviceaccounts.read");
        scopes.Should().NotContain("serviceaccounts.write");
        scopes.Should().NotContain("serviceaccounts.manage");
        roles.Should().NotContain("admin");
    }

    private async Task<AppRegistrationResponse> RegisterAppAsync(
        string clientName,
        IReadOnlyList<string> scopes)
    {
        HttpResponseMessage response = await Client.PostAsJsonAsync(
            RegisterEndpoint,
            new RegisterAppRequest(clientName, scopes));

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        AppRegistrationResponse? result = await response.Content
            .ReadFromJsonAsync<AppRegistrationResponse>();
        result.Should().NotBeNull();

        return result!;
    }

    private async Task<string?> AcquireTokenAsync(
        string clientId,
        string clientSecret,
        IReadOnlyList<string>? scopes = null)
    {
        HttpResponseMessage response = await PostTokenRequestAsync(clientId, clientSecret, scopes);
        response.EnsureSuccessStatusCode();

        TokenResponse? content = await response.Content.ReadFromJsonAsync<TokenResponse>();
        return content?.AccessToken;
    }

    private async Task<HttpResponseMessage> PostTokenRequestAsync(
        string clientId,
        string clientSecret,
        IReadOnlyList<string>? scopes = null)
    {
        HttpClient tokenClient = Factory.CreateClient();
        tokenClient.DefaultRequestHeaders.Remove("Authorization");

        Dictionary<string, string> formData = new()
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret
        };

        if (scopes is { Count: > 0 })
        {
            formData["scope"] = string.Join(' ', scopes);
        }

        using FormUrlEncodedContent content = new(formData);

        return await tokenClient.PostAsync(TokenEndpoint, content);
    }

    private sealed record RegisterAppRequest(
        string ClientName,
        IReadOnlyList<string> RequestedScopes);

    private sealed record AppRegistrationResponse(
        string ClientId,
        string ClientSecret,
        string RegistrationAccessToken);

    private sealed record TokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; init; } = string.Empty;
    }
}
