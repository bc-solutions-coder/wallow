using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Wallow.Tests.Common.Factories;

namespace Wallow.Identity.IntegrationTests.OAuth2;

/// <summary>
/// Tests OAuth2 token acquisition via OpenIddict's token endpoint.
/// Validates client credentials flow and token structure.
/// </summary>
[Trait("Category", "Integration")]
public class TokenAcquisitionTests(WallowApiFactory factory) : IdentityIntegrationTestBase(factory)
{
    [Fact]
    public async Task Should_Acquire_Token_With_Client_Credentials()
    {
        string? token = await RequestClientCredentialsTokenAsync(
            IdentityFixture.ApiClientId,
            IdentityFixture.ApiClientSecret);

        token.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Should_Fail_With_Invalid_Client_Secret()
    {
        HttpResponseMessage response = await PostTokenRequestAsync(
            IdentityFixture.ApiClientId,
            "invalid-secret");

        response.StatusCode.Should().NotBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Should_Fail_With_Invalid_Client_Id()
    {
        HttpResponseMessage response = await PostTokenRequestAsync(
            "invalid-client",
            IdentityFixture.ApiClientSecret);

        response.StatusCode.Should().NotBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Test_Service_Account_Should_Acquire_Token()
    {
        string? token = await RequestClientCredentialsTokenAsync(
            IdentityFixture.ServiceAccountClientId,
            IdentityFixture.ServiceAccountClientSecret);

        token.Should().NotBeNullOrWhiteSpace();
    }

    private async Task<string?> RequestClientCredentialsTokenAsync(string clientId, string clientSecret)
    {
        HttpResponseMessage response = await PostTokenRequestAsync(clientId, clientSecret);
        response.EnsureSuccessStatusCode();

        TokenResponse? content = await response.Content.ReadFromJsonAsync<TokenResponse>();
        return content?.AccessToken;
    }

    private async Task<HttpResponseMessage> PostTokenRequestAsync(string clientId, string clientSecret)
    {
        // Use a separate HttpClient without the default test auth header
        HttpClient tokenClient = Factory.CreateClient();
        tokenClient.DefaultRequestHeaders.Remove("Authorization");

        using FormUrlEncodedContent content = new(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
            ["scope"] = "openid"
        });
        return await tokenClient.PostAsync("/connect/token", content);
    }

    private sealed record TokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; init; } = string.Empty;
    }
}
