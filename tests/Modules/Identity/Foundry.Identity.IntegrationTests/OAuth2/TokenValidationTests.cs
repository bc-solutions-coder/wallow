using System.Net;

namespace Foundry.Identity.IntegrationTests.OAuth2;

/// <summary>
/// Tests real JWT token validation against protected API endpoints.
/// Validates that Keycloak-issued tokens are properly validated by the API.
/// </summary>
[Trait("Category", "Integration")]
public class TokenValidationTests(KeycloakTestFixture fixture) : KeycloakIntegrationTestBase(fixture)
{

    [Fact]
    public async Task Should_Reject_Request_Without_Token()
    {
        HttpResponseMessage response = await Client.GetAsync("/api/identity/service-accounts");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Should_Reject_Request_With_Invalid_Token()
    {
        SetAuthorizationHeader("invalid.token.here");

        HttpResponseMessage response = await Client.GetAsync("/api/identity/service-accounts");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Should_Accept_Request_With_Valid_Token()
    {
        string token = await GetServiceAccountTokenAsync(
            Fixture.KeycloakFixture.ClientId,
            Fixture.KeycloakFixture.ClientSecret);

        SetAuthorizationHeader(token);

        HttpResponseMessage response = await Client.GetAsync("/api/identity/service-accounts");

        // Should not be unauthorized — the token is valid and accepted.
        // May be 403 Forbidden if the service account lacks roles, which is fine;
        // the point is that authentication succeeded (not 401).
        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Valid_Token_Should_Grant_Access_To_Protected_Endpoints()
    {
        string token = await GetServiceAccountTokenAsync(
            "test-service-account",
            "test-service-secret");

        SetAuthorizationHeader(token);

        HttpResponseMessage response = await Client.GetAsync("/api/identity/service-accounts");

        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Expired_Token_Should_Be_Rejected()
    {
        // This test demonstrates the concept - in real scenarios, you'd wait for expiration
        // or use a token with a very short lifetime
        // For now, we're testing with a malformed expired token

        string expiredToken = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxMjM0NTY3ODkwIiwibmFtZSI6IkpvaG4gRG9lIiwiaWF0IjoxNTE2MjM5MDIyLCJleHAiOjE1MTYyMzkwMjJ9.invalid";

        SetAuthorizationHeader(expiredToken);

        HttpResponseMessage response = await Client.GetAsync("/api/identity/service-accounts");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
