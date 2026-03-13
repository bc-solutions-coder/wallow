using System.Net;
using System.Text;
using Foundry.Identity.Application.Interfaces;
using Foundry.Identity.Infrastructure;
using Foundry.Identity.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

#pragma warning disable CA2000 // HttpClient/HttpMessageHandler lifetime is managed by test framework

namespace Foundry.Identity.Tests.Infrastructure;

public class KeycloakTokenServiceTests
{
    private readonly ILogger<KeycloakTokenService> _logger = Substitute.For<ILogger<KeycloakTokenService>>();

    [Fact]
    public async Task GetTokenAsync_Success_ReturnsTokenResult()
    {
        string tokenJson = """
        {
            "access_token": "eyJhbGciOiJSUzI1NiJ9",
            "refresh_token": "eyJhbGciOiJSUzI1NiJ9_refresh",
            "token_type": "Bearer",
            "expires_in": 300,
            "refresh_expires_in": 1800,
            "scope": "openid profile email"
        }
        """;
        MockHttpHandler handler = new MockHttpHandler()
            .WithPost("/realms/foundry/protocol/openid-connect/token", HttpStatusCode.OK, tokenJson);

        KeycloakTokenService service = CreateService(handler);

        TokenResult result = await service.GetTokenAsync("user@test.com", "password");

        result.Success.Should().BeTrue();
        result.AccessToken.Should().Be("eyJhbGciOiJSUzI1NiJ9");
        result.RefreshToken.Should().Be("eyJhbGciOiJSUzI1NiJ9_refresh");
        result.TokenType.Should().Be("Bearer");
        result.ExpiresIn.Should().Be(300);
        result.RefreshExpiresIn.Should().Be(1800);
        result.Scope.Should().Be("openid profile email");
        result.Error.Should().BeNull();
    }

    [Fact]
    public async Task GetTokenAsync_InvalidCredentials_ReturnsError()
    {
        string errorJson = """
        {
            "error": "invalid_grant",
            "error_description": "Invalid user credentials"
        }
        """;
        MockHttpHandler handler = new MockHttpHandler()
            .WithPost("/realms/foundry/protocol/openid-connect/token", HttpStatusCode.Unauthorized, errorJson);

        KeycloakTokenService service = CreateService(handler);

        TokenResult result = await service.GetTokenAsync("user@test.com", "wrong-password");

        result.Success.Should().BeFalse();
        result.AccessToken.Should().BeNull();
        result.Error.Should().Be("invalid_grant");
        result.ErrorDescription.Should().Be("Invalid user credentials");
    }

    [Fact]
    public async Task GetTokenAsync_WhenException_ReturnsServerError()
    {
        MockHttpHandler handler = new MockHttpHandler().WithThrow();

        KeycloakTokenService service = CreateService(handler);

        TokenResult result = await service.GetTokenAsync("user@test.com", "password");

        result.Success.Should().BeFalse();
        result.Error.Should().Be("server_error");
        result.ErrorDescription.Should().Contain("error occurred");
    }

    [Fact]
    public async Task GetTokenAsync_ErrorWithInvalidJson_ReturnsUnknownError()
    {
        MockHttpHandler handler = new MockHttpHandler()
            .WithPost("/realms/foundry/protocol/openid-connect/token", HttpStatusCode.BadRequest, "not-json");

        KeycloakTokenService service = CreateService(handler);

        TokenResult result = await service.GetTokenAsync("user@test.com", "password");

        result.Success.Should().BeFalse();
        result.Error.Should().Be("unknown_error");
    }

    [Fact]
    public async Task RefreshTokenAsync_Success_ReturnsNewToken()
    {
        string tokenJson = """
        {
            "access_token": "new-access-token",
            "refresh_token": "new-refresh-token",
            "token_type": "Bearer",
            "expires_in": 300
        }
        """;
        MockHttpHandler handler = new MockHttpHandler()
            .WithPost("/realms/foundry/protocol/openid-connect/token", HttpStatusCode.OK, tokenJson);

        KeycloakTokenService service = CreateService(handler);

        TokenResult result = await service.RefreshTokenAsync("old-refresh-token");

        result.Success.Should().BeTrue();
        result.AccessToken.Should().Be("new-access-token");
        result.RefreshToken.Should().Be("new-refresh-token");
    }

    [Fact]
    public async Task RefreshTokenAsync_Failure_ReturnsError()
    {
        string errorJson = """
        {
            "error": "invalid_grant",
            "error_description": "Token is not active"
        }
        """;
        MockHttpHandler handler = new MockHttpHandler()
            .WithPost("/realms/foundry/protocol/openid-connect/token", HttpStatusCode.BadRequest, errorJson);

        KeycloakTokenService service = CreateService(handler);

        TokenResult result = await service.RefreshTokenAsync("expired-token");

        result.Success.Should().BeFalse();
        result.Error.Should().Be("invalid_grant");
    }

    [Fact]
    public async Task RefreshTokenAsync_WhenException_ReturnsServerError()
    {
        MockHttpHandler handler = new MockHttpHandler().WithThrow();

        KeycloakTokenService service = CreateService(handler);

        TokenResult result = await service.RefreshTokenAsync("some-token");

        result.Success.Should().BeFalse();
        result.Error.Should().Be("server_error");
        result.ErrorDescription.Should().Contain("refreshing");
    }

    [Fact]
    public async Task GetTokenAsync_WithMinimalResponse_HandlesOptionalFields()
    {
        string tokenJson = """
        {
            "access_token": "minimal-token"
        }
        """;
        MockHttpHandler handler = new MockHttpHandler()
            .WithPost("/realms/foundry/protocol/openid-connect/token", HttpStatusCode.OK, tokenJson);

        KeycloakTokenService service = CreateService(handler);

        TokenResult result = await service.GetTokenAsync("user@test.com", "password");

        result.Success.Should().BeTrue();
        result.AccessToken.Should().Be("minimal-token");
        result.RefreshToken.Should().BeNull();
        result.TokenType.Should().Be("Bearer"); // default
    }

    private KeycloakTokenService CreateService(HttpMessageHandler handler)
    {
        IHttpClientFactory httpClientFactory = Substitute.For<IHttpClientFactory>();
        HttpClient httpClient = new(handler)
        {
            BaseAddress = new Uri("https://keycloak.test/")
        };
        httpClientFactory.CreateClient("KeycloakTokenClient").Returns(httpClient);

        IOptions<KeycloakOptions> keycloakOptions = Options.Create(new KeycloakOptions
        {
            Realm = "foundry",
            AuthorityUrl = "https://keycloak.test/",
            AdminClientId = "foundry-api",
            AdminClientSecret = "test-secret"
        });

        return new KeycloakTokenService(
            httpClientFactory,
            keycloakOptions,
            _logger);
    }

    private sealed class MockHttpHandler : HttpMessageHandler
    {
        private readonly Dictionary<string, (HttpStatusCode Status, string Content)> _routes = new Dictionary<string, (HttpStatusCode Status, string Content)>();
        private bool _shouldThrow;

        public MockHttpHandler WithPost(string path, HttpStatusCode status, string content)
        {
            _routes[$"POST:{path}"] = (status, content);
            return this;
        }

        public MockHttpHandler WithThrow()
        {
            _shouldThrow = true;
            return this;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (_shouldThrow)
            {
                throw new HttpRequestException("Simulated failure");
            }

            string path = request.RequestUri?.AbsolutePath ?? "";
            string key = $"{request.Method}:{path}";

            if (_routes.TryGetValue(key, out (HttpStatusCode Status, string Content) route))
            {
                HttpResponseMessage response = new(route.Status)
                {
                    Content = new StringContent(route.Content, Encoding.UTF8, "application/json")
                };
                return Task.FromResult(response);
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }
}
