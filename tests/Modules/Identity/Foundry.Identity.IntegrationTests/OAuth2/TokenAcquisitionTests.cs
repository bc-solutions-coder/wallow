using System.IdentityModel.Tokens.Jwt;

namespace Foundry.Identity.IntegrationTests.OAuth2;

/// <summary>
/// Tests real OAuth2 token acquisition using Keycloak Testcontainer.
/// Validates client credentials flow and JWT token structure.
/// </summary>
[Trait("Category", "Integration")]
public class TokenAcquisitionTests(KeycloakTestFixture fixture) : KeycloakIntegrationTestBase(fixture)
{

    [Fact]
    public async Task Should_Acquire_Token_With_Client_Credentials()
    {
        string token = await GetServiceAccountTokenAsync(
            Fixture.KeycloakFixture.ClientId,
            Fixture.KeycloakFixture.ClientSecret);

        token.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Token_Should_Be_Valid_JWT()
    {
        string token = await GetServiceAccountTokenAsync(
            Fixture.KeycloakFixture.ClientId,
            Fixture.KeycloakFixture.ClientSecret);

        JwtSecurityTokenHandler handler = new();
        bool canRead = handler.CanReadToken(token);
        canRead.Should().BeTrue();

        JwtSecurityToken jwtToken = handler.ReadJwtToken(token);
        jwtToken.Should().NotBeNull();
        jwtToken.Issuer.Should().Contain(Fixture.KeycloakFixture.RealmName);
    }

    [Fact]
    public async Task Token_Should_Contain_Required_Claims()
    {
        string token = await GetServiceAccountTokenAsync(
            Fixture.KeycloakFixture.ClientId,
            Fixture.KeycloakFixture.ClientSecret);

        JwtSecurityTokenHandler handler = new();
        JwtSecurityToken jwtToken = handler.ReadJwtToken(token);

        jwtToken.Claims.Should().Contain(c => c.Type == "iss");
        jwtToken.Claims.Should().Contain(c => c.Type == "exp");
        jwtToken.Claims.Should().Contain(c => c.Type == "iat");
        jwtToken.Claims.Should().Contain(c => c.Type == "azp");
    }

    [Fact]
    public async Task Should_Fail_With_Invalid_Client_Secret()
    {
        Func<Task> act = async () => await GetServiceAccountTokenAsync(
            Fixture.KeycloakFixture.ClientId,
            "invalid-secret");

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task Should_Fail_With_Invalid_Client_Id()
    {
        Func<Task> act = async () => await GetServiceAccountTokenAsync(
            "invalid-client",
            Fixture.KeycloakFixture.ClientSecret);

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task Test_Service_Account_Should_Acquire_Token()
    {
        string token = await GetServiceAccountTokenAsync(
            "test-service-account",
            "test-service-secret");

        token.Should().NotBeNullOrWhiteSpace();

        JwtSecurityTokenHandler handler = new();
        JwtSecurityToken jwtToken = handler.ReadJwtToken(token);
        jwtToken.Claims.Should().Contain(c => c.Type == "azp" && c.Value == "test-service-account");
    }
}
