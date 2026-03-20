using System.Net;
using Wallow.Tests.Common.Factories;

namespace Wallow.Identity.IntegrationTests.OAuth2;

/// <summary>
/// Tests JWT token validation against protected API endpoints.
/// Validates that the API properly accepts/rejects tokens.
/// </summary>
[Trait("Category", "Integration")]
public class TokenValidationTests(WallowApiFactory factory) : IdentityIntegrationTestBase(factory)
{
    [Fact]
    public async Task Should_Reject_Request_Without_Token()
    {
        HttpClient unauthClient = Factory.CreateClient();
        // No Authorization header

        HttpResponseMessage response = await unauthClient.GetAsync("/api/identity/service-accounts");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Should_Reject_Request_With_Invalid_Token()
    {
        HttpClient unauthClient = Factory.CreateClient();
        unauthClient.DefaultRequestHeaders.Add("Authorization", "Bearer invalid.token.here");

        HttpResponseMessage response = await unauthClient.GetAsync("/api/identity/service-accounts");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Should_Accept_Request_With_Valid_Test_Token()
    {
        // Client already has test auth header via base class
        HttpResponseMessage response = await Client.GetAsync("/api/identity/service-accounts");

        // Should not be 401 — TestAuthHandler accepts the token
        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Expired_Token_Should_Be_Rejected()
    {
        HttpClient unauthClient = Factory.CreateClient();
        string expiredToken = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxMjM0NTY3ODkwIiwibmFtZSI6IkpvaG4gRG9lIiwiaWF0IjoxNTE2MjM5MDIyLCJleHAiOjE1MTYyMzkwMjJ9.invalid";
        unauthClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {expiredToken}");

        HttpResponseMessage response = await unauthClient.GetAsync("/api/identity/service-accounts");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
