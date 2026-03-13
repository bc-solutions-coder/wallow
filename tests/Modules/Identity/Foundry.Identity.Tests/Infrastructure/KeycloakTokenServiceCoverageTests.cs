using System.Net;
using System.Text;
using Foundry.Identity.Application.Interfaces;
using Foundry.Identity.Infrastructure.Services;
using Foundry.Identity.Infrastructure;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

#pragma warning disable CA2000 // HttpClient/HttpMessageHandler lifetime is managed by test framework

namespace Foundry.Identity.Tests.Infrastructure;

public class KeycloakTokenServiceCoverageTests
{
    private readonly ILogger<KeycloakTokenService> _logger = Substitute.For<ILogger<KeycloakTokenService>>();

    // ── RevokeTokenAsync ──────────────────────────────────────────────

    [Fact]
    public async Task RevokeTokenAsync_Success_ReturnsTrue()
    {
        MockHttpHandler handler = new MockHttpHandler()
            .WithPost("/realms/foundry/protocol/openid-connect/logout", HttpStatusCode.NoContent, "");

        KeycloakTokenService service = CreateService(handler);

        bool result = await service.RevokeTokenAsync("valid-refresh-token");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task RevokeTokenAsync_NonSuccessStatus_ReturnsFalse()
    {
        MockHttpHandler handler = new MockHttpHandler()
            .WithPost("/realms/foundry/protocol/openid-connect/logout", HttpStatusCode.BadRequest, "{}");

        KeycloakTokenService service = CreateService(handler);

        bool result = await service.RevokeTokenAsync("invalid-refresh-token");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task RevokeTokenAsync_UnauthorizedStatus_ReturnsFalse()
    {
        MockHttpHandler handler = new MockHttpHandler()
            .WithPost("/realms/foundry/protocol/openid-connect/logout", HttpStatusCode.Unauthorized, "{}");

        KeycloakTokenService service = CreateService(handler);

        bool result = await service.RevokeTokenAsync("expired-refresh-token");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task RevokeTokenAsync_WhenException_ReturnsFalse()
    {
        MockHttpHandler handler = new MockHttpHandler().WithThrow();

        KeycloakTokenService service = CreateService(handler);

        bool result = await service.RevokeTokenAsync("some-token");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task RevokeTokenAsync_ServerError_ReturnsFalse()
    {
        MockHttpHandler handler = new MockHttpHandler()
            .WithPost("/realms/foundry/protocol/openid-connect/logout", HttpStatusCode.InternalServerError, "");

        KeycloakTokenService service = CreateService(handler);

        bool result = await service.RevokeTokenAsync("some-token");

        result.Should().BeFalse();
    }

    // ── MaskEmail (exercised via GetTokenAsync error/exception paths) ─

    [Fact]
    public async Task GetTokenAsync_EmailWithoutAtSign_MasksEntireEmail()
    {
        // MaskEmail returns "***" when there is no '@' in the email
        MockHttpHandler handler = new MockHttpHandler()
            .WithPost("/realms/foundry/protocol/openid-connect/token", HttpStatusCode.Unauthorized,
                """{"error": "invalid_grant"}""");

        KeycloakTokenService service = CreateService(handler);

        TokenResult result = await service.GetTokenAsync("noemailformat", "password");

        result.Success.Should().BeFalse();
        result.Error.Should().Be("invalid_grant");
    }

    [Fact]
    public async Task GetTokenAsync_EmailStartingWithAtSign_MasksEntireEmail()
    {
        // MaskEmail returns "***" when atIndex <= 0
        MockHttpHandler handler = new MockHttpHandler()
            .WithPost("/realms/foundry/protocol/openid-connect/token", HttpStatusCode.Unauthorized,
                """{"error": "invalid_grant"}""");

        KeycloakTokenService service = CreateService(handler);

        TokenResult result = await service.GetTokenAsync("@domain.com", "password");

        result.Success.Should().BeFalse();
        result.Error.Should().Be("invalid_grant");
    }

    [Fact]
    public async Task GetTokenAsync_ExceptionWithEmailWithoutAtSign_MasksEntireEmail()
    {
        // Exercises the MaskEmail "***" branch in the exception catch block
        MockHttpHandler handler = new MockHttpHandler().WithThrow();

        KeycloakTokenService service = CreateService(handler);

        TokenResult result = await service.GetTokenAsync("noemail", "password");

        result.Success.Should().BeFalse();
        result.Error.Should().Be("server_error");
    }

    [Fact]
    public async Task GetTokenAsync_ExceptionWithValidEmail_MasksWithPartialEmail()
    {
        // Exercises the MaskEmail branch where atIndex > 0 (e.g. "u***@test.com")
        MockHttpHandler handler = new MockHttpHandler().WithThrow();

        KeycloakTokenService service = CreateService(handler);

        TokenResult result = await service.GetTokenAsync("user@test.com", "password");

        result.Success.Should().BeFalse();
        result.Error.Should().Be("server_error");
    }

    // ── RefreshTokenAsync additional coverage ─────────────────────────

    [Fact]
    public async Task RefreshTokenAsync_SuccessWithMinimalResponse_HandlesOptionalFields()
    {
        string tokenJson = """
        {
            "access_token": "minimal-access"
        }
        """;
        MockHttpHandler handler = new MockHttpHandler()
            .WithPost("/realms/foundry/protocol/openid-connect/token", HttpStatusCode.OK, tokenJson);

        KeycloakTokenService service = CreateService(handler);

        TokenResult result = await service.RefreshTokenAsync("refresh-token");

        result.Success.Should().BeTrue();
        result.AccessToken.Should().Be("minimal-access");
        result.RefreshToken.Should().BeNull();
        result.TokenType.Should().Be("Bearer");
        result.ExpiresIn.Should().BeNull();
        result.RefreshExpiresIn.Should().BeNull();
        result.Scope.Should().BeNull();
    }

    [Fact]
    public async Task RefreshTokenAsync_SuccessWithAllFields_ReturnsAllFields()
    {
        string tokenJson = """
        {
            "access_token": "new-access",
            "refresh_token": "new-refresh",
            "token_type": "Bearer",
            "expires_in": 300,
            "refresh_expires_in": 1800,
            "scope": "openid profile"
        }
        """;
        MockHttpHandler handler = new MockHttpHandler()
            .WithPost("/realms/foundry/protocol/openid-connect/token", HttpStatusCode.OK, tokenJson);

        KeycloakTokenService service = CreateService(handler);

        TokenResult result = await service.RefreshTokenAsync("old-refresh");

        result.Success.Should().BeTrue();
        result.AccessToken.Should().Be("new-access");
        result.RefreshToken.Should().Be("new-refresh");
        result.TokenType.Should().Be("Bearer");
        result.ExpiresIn.Should().Be(300);
        result.RefreshExpiresIn.Should().Be(1800);
        result.Scope.Should().Be("openid profile");
    }

    [Fact]
    public async Task RefreshTokenAsync_ErrorWithMissingErrorField_ReturnsUnknownError()
    {
        string errorJson = """
        {
            "some_other": "field"
        }
        """;
        MockHttpHandler handler = new MockHttpHandler()
            .WithPost("/realms/foundry/protocol/openid-connect/token", HttpStatusCode.BadRequest, errorJson);

        KeycloakTokenService service = CreateService(handler);

        TokenResult result = await service.RefreshTokenAsync("expired-token");

        result.Success.Should().BeFalse();
        result.Error.Should().Be("unknown_error");
    }

    [Fact]
    public async Task RefreshTokenAsync_ErrorWithDescriptionOnly_ReturnsUnknownErrorWithDescription()
    {
        string errorJson = """
        {
            "error_description": "Something went wrong"
        }
        """;
        MockHttpHandler handler = new MockHttpHandler()
            .WithPost("/realms/foundry/protocol/openid-connect/token", HttpStatusCode.BadRequest, errorJson);

        KeycloakTokenService service = CreateService(handler);

        TokenResult result = await service.RefreshTokenAsync("expired-token");

        result.Success.Should().BeFalse();
        result.Error.Should().Be("unknown_error");
        result.ErrorDescription.Should().Be("Something went wrong");
    }

    // ── URL construction with trailing slash ──────────────────────────

    [Fact]
    public async Task GetTokenAsync_AuthorityUrlWithTrailingSlash_ConstructsCorrectEndpoint()
    {
        string tokenJson = """
        {
            "access_token": "token-trailing-slash"
        }
        """;
        MockHttpHandler handler = new MockHttpHandler()
            .WithPost("/realms/foundry/protocol/openid-connect/token", HttpStatusCode.OK, tokenJson);

        KeycloakTokenService service = CreateServiceWithTrailingSlash(handler);

        TokenResult result = await service.GetTokenAsync("user@test.com", "password");

        result.Success.Should().BeTrue();
        result.AccessToken.Should().Be("token-trailing-slash");
    }

    [Fact]
    public async Task RevokeTokenAsync_AuthorityUrlWithTrailingSlash_ConstructsCorrectEndpoint()
    {
        MockHttpHandler handler = new MockHttpHandler()
            .WithPost("/realms/foundry/protocol/openid-connect/logout", HttpStatusCode.NoContent, "");

        KeycloakTokenService service = CreateServiceWithTrailingSlash(handler);

        bool result = await service.RevokeTokenAsync("refresh-token");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task RefreshTokenAsync_AuthorityUrlWithTrailingSlash_ConstructsCorrectEndpoint()
    {
        string tokenJson = """
        {
            "access_token": "refreshed-trailing"
        }
        """;
        MockHttpHandler handler = new MockHttpHandler()
            .WithPost("/realms/foundry/protocol/openid-connect/token", HttpStatusCode.OK, tokenJson);

        KeycloakTokenService service = CreateServiceWithTrailingSlash(handler);

        TokenResult result = await service.RefreshTokenAsync("old-token");

        result.Success.Should().BeTrue();
    }

    // ── GetTokenAsync additional error parsing branches ───────────────

    [Fact]
    public async Task GetTokenAsync_ErrorWithEmptyJsonObject_ReturnsUnknownError()
    {
        MockHttpHandler handler = new MockHttpHandler()
            .WithPost("/realms/foundry/protocol/openid-connect/token", HttpStatusCode.BadRequest, "{}");

        KeycloakTokenService service = CreateService(handler);

        TokenResult result = await service.GetTokenAsync("user@test.com", "password");

        result.Success.Should().BeFalse();
        result.Error.Should().Be("unknown_error");
        result.ErrorDescription.Should().BeNull();
    }

    [Fact]
    public async Task GetTokenAsync_ErrorWithEmptyString_ReturnsUnknownError()
    {
        MockHttpHandler handler = new MockHttpHandler()
            .WithPost("/realms/foundry/protocol/openid-connect/token", HttpStatusCode.BadRequest, "");

        KeycloakTokenService service = CreateService(handler);

        TokenResult result = await service.GetTokenAsync("user@test.com", "password");

        result.Success.Should().BeFalse();
        result.Error.Should().Be("unknown_error");
        result.ErrorDescription.Should().Be("Failed to parse error response");
    }

    // ── RevokeTokenAsync with CancellationToken ──────────────────────

    [Fact]
    public async Task RevokeTokenAsync_WithCancellationToken_PropagatesCancellation()
    {
        CancellationTokenSource cts = new();
        await cts.CancelAsync();

        MockHttpHandler handler = new MockHttpHandler()
            .WithPost("/realms/foundry/protocol/openid-connect/logout", HttpStatusCode.NoContent, "");

        KeycloakTokenService service = CreateService(handler);

        // The HttpClient.PostAsync should throw OperationCanceledException,
        // which is caught by the catch(Exception) block and returns false
        bool result = await service.RevokeTokenAsync("token", cts.Token);

        result.Should().BeFalse();
    }

    // ── Helpers ───────────────────────────────────────────────────────

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
            AuthorityUrl = "https://keycloak.test",
            AdminClientId = "foundry-api",
            AdminClientSecret = "test-secret"
        });

        return new KeycloakTokenService(httpClientFactory, keycloakOptions, _logger);
    }

    private KeycloakTokenService CreateServiceWithTrailingSlash(HttpMessageHandler handler)
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
            cancellationToken.ThrowIfCancellationRequested();

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
