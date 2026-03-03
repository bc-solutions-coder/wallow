using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace Foundry.Identity.IntegrationTests.OAuth2;

/// <summary>
/// End-to-end tests for service account OAuth2 flows.
/// Validates the complete flow: acquire token -> call API -> verify response.
/// </summary>
[Trait("Category", "Integration")]
public class ServiceAccountFlowTests : KeycloakIntegrationTestBase
{
    public ServiceAccountFlowTests(KeycloakTestFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Complete_Flow_Acquire_Token_And_Call_API()
    {
        // Step 1: Acquire token from Keycloak
        string token = await GetServiceAccountTokenAsync(
            Fixture.KeycloakFixture.ClientId,
            Fixture.KeycloakFixture.ClientSecret);

        token.Should().NotBeNullOrWhiteSpace();

        // Step 2: Use token to call protected API
        SetAuthorizationHeader(token);
        HttpResponseMessage response = await Client.GetAsync("/api/identity/service-accounts");

        // Step 3: Verify successful authentication
        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Token_Should_Work_For_Multiple_Requests()
    {
        string token = await GetServiceAccountTokenAsync(
            Fixture.KeycloakFixture.ClientId,
            Fixture.KeycloakFixture.ClientSecret);

        SetAuthorizationHeader(token);

        // First request
        HttpResponseMessage response1 = await Client.GetAsync("/api/identity/service-accounts");
        response1.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);

        // Second request with same token
        HttpResponseMessage response2 = await Client.GetAsync("/api/identity/service-accounts");
        response2.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Different_Service_Accounts_Should_Get_Different_Tokens()
    {
        string token1 = await GetServiceAccountTokenAsync(
            Fixture.KeycloakFixture.ClientId,
            Fixture.KeycloakFixture.ClientSecret);

        string token2 = await GetServiceAccountTokenAsync(
            "test-service-account",
            "test-service-secret");

        token1.Should().NotBeNullOrWhiteSpace();
        token2.Should().NotBeNullOrWhiteSpace();
        token1.Should().NotBe(token2);

        JwtSecurityTokenHandler handler = new();
        JwtSecurityToken jwt1 = handler.ReadJwtToken(token1);
        JwtSecurityToken jwt2 = handler.ReadJwtToken(token2);

        string? clientId1 = jwt1.Claims.FirstOrDefault(c => c.Type == "azp")?.Value;
        string? clientId2 = jwt2.Claims.FirstOrDefault(c => c.Type == "azp")?.Value;

        clientId1.Should().Be(Fixture.KeycloakFixture.ClientId);
        clientId2.Should().Be("test-service-account");
    }

    [Fact]
    public async Task Direct_Token_Endpoint_Call_Should_Return_Valid_Token()
    {
        using HttpClient httpClient = new();
        string tokenEndpoint = Fixture.KeycloakFixture.TokenEndpoint;

        using HttpRequestMessage request = new(HttpMethod.Post, tokenEndpoint)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = Fixture.KeycloakFixture.ClientId,
                ["client_secret"] = Fixture.KeycloakFixture.ClientSecret
            })
        };

        HttpResponseMessage response = await httpClient.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        TokenResponse? content = await response.Content.ReadFromJsonAsync<TokenResponse>();
        content.Should().NotBeNull();
        content.AccessToken.Should().NotBeNullOrWhiteSpace();
        content.TokenType.Should().Be("Bearer");
        content.ExpiresIn.Should().BeGreaterThan(0);
    }

    private sealed record TokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; init; } = string.Empty;

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; init; }

        [JsonPropertyName("token_type")]
        public string TokenType { get; init; } = string.Empty;

        [JsonPropertyName("scope")]
        public string Scope { get; init; } = string.Empty;
    }
}
