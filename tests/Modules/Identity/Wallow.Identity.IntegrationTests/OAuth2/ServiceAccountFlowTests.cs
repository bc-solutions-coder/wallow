using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Wallow.Tests.Common.Factories;

namespace Wallow.Identity.IntegrationTests.OAuth2;

/// <summary>
/// End-to-end tests for service account OAuth2 flows via OpenIddict.
/// Validates the complete flow: acquire token -> call API -> verify response.
/// </summary>
[Trait("Category", "Integration")]
public class ServiceAccountFlowTests(WallowApiFactory factory) : IdentityIntegrationTestBase(factory)
{
    [Fact]
    public async Task Complete_Flow_Acquire_Token_And_Call_API()
    {
        // Step 1: Acquire token from OpenIddict
        string? token = await RequestClientCredentialsTokenAsync(
            IdentityFixture.ApiClientId,
            IdentityFixture.ApiClientSecret);
        token.Should().NotBeNullOrWhiteSpace();

        // Step 2: Use token to call protected API
        HttpClient apiClient = Factory.CreateClient();
        apiClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
        HttpResponseMessage response = await apiClient.GetAsync("/api/identity/service-accounts");

        // Step 3: Verify successful authentication (not 401)
        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Different_Service_Accounts_Should_Get_Different_Tokens()
    {
        string? token1 = await RequestClientCredentialsTokenAsync(
            IdentityFixture.ApiClientId,
            IdentityFixture.ApiClientSecret);

        string? token2 = await RequestClientCredentialsTokenAsync(
            IdentityFixture.ServiceAccountClientId,
            IdentityFixture.ServiceAccountClientSecret);

        token1.Should().NotBeNullOrWhiteSpace();
        token2.Should().NotBeNullOrWhiteSpace();
        token1.Should().NotBe(token2);
    }

    [Fact]
    public async Task Direct_Token_Endpoint_Call_Should_Return_Valid_Token()
    {
        HttpClient tokenClient = Factory.CreateClient();
        tokenClient.DefaultRequestHeaders.Remove("Authorization");

        using FormUrlEncodedContent tokenContent = new(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = IdentityFixture.ApiClientId,
            ["client_secret"] = IdentityFixture.ApiClientSecret,
            ["scope"] = "openid"
        });
        HttpResponseMessage response = await tokenClient.PostAsync("/connect/token", tokenContent);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        TokenResponse? content = await response.Content.ReadFromJsonAsync<TokenResponse>();
        content.Should().NotBeNull();
        content!.AccessToken.Should().NotBeNullOrWhiteSpace();
        content.TokenType.Should().Be("Bearer");
        content.ExpiresIn.Should().BeGreaterThan(0);
    }

    private async Task<string?> RequestClientCredentialsTokenAsync(string clientId, string clientSecret)
    {
        HttpClient tokenClient = Factory.CreateClient();
        tokenClient.DefaultRequestHeaders.Remove("Authorization");

        using FormUrlEncodedContent credContent = new(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
            ["scope"] = "openid"
        });
        HttpResponseMessage response = await tokenClient.PostAsync("/connect/token", credContent);

        response.EnsureSuccessStatusCode();
        TokenResponse? content = await response.Content.ReadFromJsonAsync<TokenResponse>();
        return content?.AccessToken;
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
