using System.Net;
using Wallow.Tests.Common.Factories;

namespace Wallow.Identity.IntegrationTests.OAuth2;

/// <summary>
/// Checks missing and malformed bearer refusals, plus admission through synthetic test authentication.
/// The expired-token fixture also has an invalid signature, so it does not isolate expiry validation.
/// </summary>
[Trait("Category", "Integration")]
public class TokenValidationTests(WallowApiFactory factory) : IdentityIntegrationTestBase(factory)
{
    [Fact]
    public async Task Should_Reject_Request_Without_Token()
    {
        HttpClient unauthClient = Factory.CreateClient();


        HttpResponseMessage response = await unauthClient.GetAsync("/identity/clients");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Should_Reject_Request_With_Invalid_Token()
    {
        HttpClient unauthClient = Factory.CreateClient();
        unauthClient.DefaultRequestHeaders.Add("Authorization", "Bearer invalid.token.here");
        unauthClient.DefaultRequestHeaders.Add("X-Test-Auth-Skip", "true");

        HttpResponseMessage response = await unauthClient.GetAsync("/identity/clients");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Should_Accept_Request_With_Valid_Test_Token()
    {
        // The base client supplies synthetic authentication headers.
        HttpResponseMessage response = await Client.GetAsync("/identity/clients");


        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Expired_Token_Should_Be_Rejected()
    {
        HttpClient unauthClient = Factory.CreateClient();
        string expiredToken = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxMjM0NTY3ODkwIiwibmFtZSI6IkpvaG4gRG9lIiwiaWF0IjoxNTE2MjM5MDIyLCJleHAiOjE1MTYyMzkwMjJ9.invalid";
        unauthClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {expiredToken}");
        unauthClient.DefaultRequestHeaders.Add("X-Test-Auth-Skip", "true");

        HttpResponseMessage response = await unauthClient.GetAsync("/identity/clients");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
