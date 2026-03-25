using System.Net;
using RichardSzalay.MockHttp;
using Wallow.Auth.Models;
using Wallow.Auth.Services;

namespace Wallow.Auth.Tests.Services;

public sealed class AuthApiClientTests : IDisposable
{
    private readonly MockHttpMessageHandler _mockHttp = new();
    private readonly AuthApiClient _sut;

    public AuthApiClientTests()
    {
        HttpClient httpClient = _mockHttp.ToHttpClient();
        httpClient.BaseAddress = new Uri("http://localhost:5000");

        IHttpClientFactory factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("AuthApi").Returns(httpClient);

        _sut = new AuthApiClient(factory);
    }

    public void Dispose()
    {
        _mockHttp.Dispose();
    }

    [Fact]
    public async Task LoginAsync_ValidCredentials_ReturnsSuccess()
    {
        _mockHttp.When(HttpMethod.Post, "http://localhost:5000/api/v1/identity/auth/login")
            .Respond("application/json", """{"succeeded":true}""");

        AuthResponse result = await _sut.LoginAsync(new LoginRequest("test@test.com", "password", false));

        result.Succeeded.Should().BeTrue();
        result.Error.Should().BeNull();
    }

    [Fact]
    public async Task LoginAsync_InvalidCredentials_ReturnsError()
    {
        _mockHttp.When(HttpMethod.Post, "http://localhost:5000/api/v1/identity/auth/login")
            .Respond(HttpStatusCode.Unauthorized, "application/json",
                """{"succeeded":false,"error":"invalid_credentials"}""");

        AuthResponse result = await _sut.LoginAsync(new LoginRequest("test@test.com", "wrong", false));

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Be("invalid_credentials");
    }

    [Fact]
    public async Task LoginAsync_LockedOut_ReturnsLockedOutError()
    {
        _mockHttp.When(HttpMethod.Post, "http://localhost:5000/api/v1/identity/auth/login")
            .Respond((HttpStatusCode)423, "application/json",
                """{"succeeded":false,"error":"locked_out"}""");

        AuthResponse result = await _sut.LoginAsync(new LoginRequest("test@test.com", "password", false));

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Be("locked_out");
    }

    [Fact]
    public async Task RegisterAsync_ValidRequest_ReturnsSuccess()
    {
        _mockHttp.When(HttpMethod.Post, "http://localhost:5000/api/v1/identity/auth/register")
            .Respond("application/json", """{"succeeded":true}""");

        AuthResponse result = await _sut.RegisterAsync(
            new RegisterRequest("test@test.com", "Password1!", "Password1!"));

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task RegisterAsync_DuplicateEmail_ReturnsError()
    {
        _mockHttp.When(HttpMethod.Post, "http://localhost:5000/api/v1/identity/auth/register")
            .Respond(HttpStatusCode.BadRequest, "application/json",
                """{"succeeded":false,"error":"email_taken"}""");

        AuthResponse result = await _sut.RegisterAsync(
            new RegisterRequest("taken@test.com", "Password1!", "Password1!"));

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Be("email_taken");
    }

    [Fact]
    public async Task ForgotPasswordAsync_AnyEmail_ReturnsSuccess()
    {
        _mockHttp.When(HttpMethod.Post, "http://localhost:5000/api/v1/identity/auth/forgot-password")
            .Respond("application/json", """{"succeeded":true}""");

        AuthResponse result = await _sut.ForgotPasswordAsync(new ForgotPasswordRequest("any@test.com"));

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task VerifyEmailAsync_ValidToken_ReturnsSuccess()
    {
        _mockHttp.When(HttpMethod.Get, "http://localhost:5000/api/v1/identity/auth/verify-email*")
            .Respond("application/json", """{"succeeded":true}""");

        AuthResponse result = await _sut.VerifyEmailAsync("test@test.com", "valid-token");

        result.Succeeded.Should().BeTrue();
    }
}
