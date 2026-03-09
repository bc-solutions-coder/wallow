using System.Net;
using System.Text;
using Foundry.Identity.Application.Interfaces;
using Foundry.Identity.Infrastructure.Services;
using Foundry.Identity.Infrastructure;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

#pragma warning disable CA2000 // HttpClient/HttpMessageHandler lifetime is managed by test framework

namespace Foundry.Identity.Tests.Infrastructure;

public class KeycloakTokenServiceGapTests
{
    private readonly ILogger<KeycloakTokenService> _logger = Substitute.For<ILogger<KeycloakTokenService>>();

    [Fact]
    public async Task GetTokenAsync_UsesConfigDefaults_WhenConfigMissing()
    {
        string tokenJson = """
        {
            "access_token": "default-token"
        }
        """;
        MockHttpHandler handler = new MockHttpHandler()
            .WithPost("/realms/foundry/protocol/openid-connect/token", HttpStatusCode.OK, tokenJson);

        IOptions<KeycloakOptions> keycloakOptions = Options.Create(new KeycloakOptions());

        IHttpClientFactory httpClientFactory = Substitute.For<IHttpClientFactory>();
        HttpClient httpClient = new(handler)
        {
            BaseAddress = new Uri("http://localhost:8080/")
        };
        httpClientFactory.CreateClient("KeycloakTokenClient").Returns(httpClient);

        KeycloakTokenService service = new(httpClientFactory, keycloakOptions, _logger);

        TokenResult result = await service.GetTokenAsync("user@test.com", "password");

        result.Success.Should().BeTrue();
        result.AccessToken.Should().Be("default-token");
    }

    [Fact]
    public async Task RefreshTokenAsync_ErrorWithInvalidJson_ReturnsUnknownError()
    {
        MockHttpHandler handler = new MockHttpHandler()
            .WithPost("/realms/foundry/protocol/openid-connect/token", HttpStatusCode.BadRequest, "not-json");

        KeycloakTokenService service = CreateService(handler);

        TokenResult result = await service.RefreshTokenAsync("expired-token");

        result.Success.Should().BeFalse();
        result.Error.Should().Be("unknown_error");
    }

    [Fact]
    public async Task GetTokenAsync_WithErrorResponseMissingDescription_ReturnsNullDescription()
    {
        string errorJson = """
        {
            "error": "invalid_client"
        }
        """;
        MockHttpHandler handler = new MockHttpHandler()
            .WithPost("/realms/foundry/protocol/openid-connect/token", HttpStatusCode.Unauthorized, errorJson);

        KeycloakTokenService service = CreateService(handler);

        TokenResult result = await service.GetTokenAsync("user@test.com", "password");

        result.Success.Should().BeFalse();
        result.Error.Should().Be("invalid_client");
        result.ErrorDescription.Should().BeNull();
    }

    [Fact]
    public async Task GetTokenAsync_WithErrorResponseMissingError_ReturnsUnknownError()
    {
        string errorJson = """
        {
            "other_field": "value"
        }
        """;
        MockHttpHandler handler = new MockHttpHandler()
            .WithPost("/realms/foundry/protocol/openid-connect/token", HttpStatusCode.BadRequest, errorJson);

        KeycloakTokenService service = CreateService(handler);

        TokenResult result = await service.GetTokenAsync("user@test.com", "password");

        result.Success.Should().BeFalse();
        result.Error.Should().Be("unknown_error");
    }

    [Fact]
    public async Task GetTokenAsync_SuccessWithAllOptionalFields_ReturnsAllFields()
    {
        string tokenJson = """
        {
            "access_token": "full-token",
            "refresh_token": "refresh",
            "token_type": "Bearer",
            "expires_in": 600,
            "refresh_expires_in": 3600,
            "scope": "openid"
        }
        """;
        MockHttpHandler handler = new MockHttpHandler()
            .WithPost("/realms/foundry/protocol/openid-connect/token", HttpStatusCode.OK, tokenJson);

        KeycloakTokenService service = CreateService(handler);

        TokenResult result = await service.GetTokenAsync("user@test.com", "password");

        result.Success.Should().BeTrue();
        result.AccessToken.Should().Be("full-token");
        result.RefreshToken.Should().Be("refresh");
        result.TokenType.Should().Be("Bearer");
        result.ExpiresIn.Should().Be(600);
        result.RefreshExpiresIn.Should().Be(3600);
        result.Scope.Should().Be("openid");
        result.Error.Should().BeNull();
        result.ErrorDescription.Should().BeNull();
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

        return new KeycloakTokenService(httpClientFactory, keycloakOptions, _logger);
    }

    private sealed class MockHttpHandler : HttpMessageHandler
    {
        private readonly Dictionary<string, (HttpStatusCode Status, string Content)> _routes = new Dictionary<string, (HttpStatusCode Status, string Content)>();

        public MockHttpHandler WithPost(string path, HttpStatusCode status, string content)
        {
            _routes[$"POST:{path}"] = (status, content);
            return this;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
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
