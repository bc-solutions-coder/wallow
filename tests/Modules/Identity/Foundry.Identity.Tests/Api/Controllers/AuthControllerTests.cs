using Foundry.Identity.Api.Contracts.Requests;
using Foundry.Identity.Api.Contracts.Responses;
using Foundry.Identity.Api.Controllers;
using Foundry.Identity.Application.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Foundry.Identity.Tests.Api.Controllers;

public class AuthControllerTests
{
    private readonly ITokenService _tokenService;
    private readonly AuthController _controller;

    public AuthControllerTests()
    {
        _tokenService = Substitute.For<ITokenService>();
        _controller = new AuthController(_tokenService, Substitute.For<ILogger<AuthController>>());
    }

    #region GetToken

    [Fact]
    public async Task GetToken_WithValidCredentials_ReturnsOkWithTokenResponse()
    {
        TokenRequest request = new("user@example.com", "password123");
        TokenResult tokenResult = new(true, "access-token", "refresh-token", "Bearer", 300, 1800, "openid", null, null);
        _tokenService.GetTokenAsync("user@example.com", "password123", Arg.Any<CancellationToken>())
            .Returns(tokenResult);

        IActionResult result = await _controller.GetToken(request, CancellationToken.None);

        OkObjectResult ok = result.Should().BeOfType<OkObjectResult>().Subject;
        TokenResponse response = ok.Value.Should().BeOfType<TokenResponse>().Subject;
        response.AccessToken.Should().Be("access-token");
        response.RefreshToken.Should().Be("refresh-token");
        response.TokenType.Should().Be("Bearer");
        response.ExpiresIn.Should().Be(300);
        response.RefreshExpiresIn.Should().Be(1800);
        response.Scope.Should().Be("openid");
    }

    [Fact]
    public async Task GetToken_WithEmptyEmail_ReturnsBadRequest()
    {
        TokenRequest request = new("", "password123");

        IActionResult result = await _controller.GetToken(request, CancellationToken.None);

        BadRequestObjectResult badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        ProblemDetails problem = badRequest.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Detail.Should().Be("Email and password are required");
    }

    [Fact]
    public async Task GetToken_WithWhitespaceEmail_ReturnsBadRequest()
    {
        TokenRequest request = new("   ", "password123");

        IActionResult result = await _controller.GetToken(request, CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task GetToken_WithEmptyPassword_ReturnsBadRequest()
    {
        TokenRequest request = new("user@example.com", "");

        IActionResult result = await _controller.GetToken(request, CancellationToken.None);

        BadRequestObjectResult badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        ProblemDetails problem = badRequest.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Status.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task GetToken_WithWhitespacePassword_ReturnsBadRequest()
    {
        TokenRequest request = new("user@example.com", "   ");

        IActionResult result = await _controller.GetToken(request, CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task GetToken_WithInvalidCredentials_ReturnsUnauthorized()
    {
        TokenRequest request = new("user@example.com", "wrong-password");
        TokenResult tokenResult = new(false, null, null, null, null, null, null, "invalid_grant", "Invalid credentials");
        _tokenService.GetTokenAsync("user@example.com", "wrong-password", Arg.Any<CancellationToken>())
            .Returns(tokenResult);

        IActionResult result = await _controller.GetToken(request, CancellationToken.None);

        UnauthorizedObjectResult unauthorized = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        ProblemDetails problem = unauthorized.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Title.Should().Be("Authentication failed");
        problem.Detail.Should().Be("Invalid credentials");
        problem.Status.Should().Be(StatusCodes.Status401Unauthorized);
        problem.Extensions.Should().ContainKey("error");
    }

    [Fact]
    public async Task GetToken_WhenFailedWithErrorOnly_UsesErrorAsDetail()
    {
        TokenRequest request = new("user@example.com", "password");
        TokenResult tokenResult = new(false, null, null, null, null, null, null, "server_error", null);
        _tokenService.GetTokenAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(tokenResult);

        IActionResult result = await _controller.GetToken(request, CancellationToken.None);

        UnauthorizedObjectResult unauthorized = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        ProblemDetails problem = unauthorized.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Detail.Should().Be("server_error");
    }

    [Fact]
    public async Task GetToken_WhenFailedWithNoErrorInfo_UsesDefaultMessage()
    {
        TokenRequest request = new("user@example.com", "password");
        TokenResult tokenResult = new(false, null, null, null, null, null, null, null, null);
        _tokenService.GetTokenAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(tokenResult);

        IActionResult result = await _controller.GetToken(request, CancellationToken.None);

        UnauthorizedObjectResult unauthorized = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        ProblemDetails problem = unauthorized.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Detail.Should().Be("Invalid credentials");
    }

    [Fact]
    public async Task GetToken_WhenSuccessWithNullTokenType_DefaultsToBearer()
    {
        TokenRequest request = new("user@example.com", "password");
        TokenResult tokenResult = new(true, "access-token", null, null, null, null, null, null, null);
        _tokenService.GetTokenAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(tokenResult);

        IActionResult result = await _controller.GetToken(request, CancellationToken.None);

        OkObjectResult ok = result.Should().BeOfType<OkObjectResult>().Subject;
        TokenResponse response = ok.Value.Should().BeOfType<TokenResponse>().Subject;
        response.TokenType.Should().Be("Bearer");
        response.ExpiresIn.Should().Be(300);
    }

    [Fact]
    public async Task GetToken_PassesCorrectCredentialsToService()
    {
        TokenRequest request = new("test@test.com", "secret123");
        TokenResult tokenResult = new(true, "token", null, "Bearer", 300, null, null, null, null);
        _tokenService.GetTokenAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(tokenResult);

        await _controller.GetToken(request, CancellationToken.None);

        await _tokenService.Received(1).GetTokenAsync("test@test.com", "secret123", Arg.Any<CancellationToken>());
    }

    #endregion

    #region RefreshToken

    [Fact]
    public async Task RefreshToken_WithValidToken_ReturnsOkWithTokenResponse()
    {
        RefreshTokenRequest request = new("valid-refresh-token");
        TokenResult tokenResult = new(true, "new-access-token", "new-refresh-token", "Bearer", 300, 1800, "openid", null, null);
        _tokenService.RefreshTokenAsync("valid-refresh-token", Arg.Any<CancellationToken>())
            .Returns(tokenResult);

        IActionResult result = await _controller.RefreshToken(request, CancellationToken.None);

        OkObjectResult ok = result.Should().BeOfType<OkObjectResult>().Subject;
        TokenResponse response = ok.Value.Should().BeOfType<TokenResponse>().Subject;
        response.AccessToken.Should().Be("new-access-token");
        response.RefreshToken.Should().Be("new-refresh-token");
    }

    [Fact]
    public async Task RefreshToken_WithEmptyToken_ReturnsBadRequest()
    {
        RefreshTokenRequest request = new("");

        IActionResult result = await _controller.RefreshToken(request, CancellationToken.None);

        BadRequestObjectResult badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        ProblemDetails problem = badRequest.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Detail.Should().Be("Refresh token is required");
    }

    [Fact]
    public async Task RefreshToken_WithWhitespaceToken_ReturnsBadRequest()
    {
        RefreshTokenRequest request = new("   ");

        IActionResult result = await _controller.RefreshToken(request, CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task RefreshToken_WithExpiredToken_ReturnsUnauthorized()
    {
        RefreshTokenRequest request = new("expired-token");
        TokenResult tokenResult = new(false, null, null, null, null, null, null, "invalid_grant", "Token has expired");
        _tokenService.RefreshTokenAsync("expired-token", Arg.Any<CancellationToken>())
            .Returns(tokenResult);

        IActionResult result = await _controller.RefreshToken(request, CancellationToken.None);

        UnauthorizedObjectResult unauthorized = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        ProblemDetails problem = unauthorized.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Title.Should().Be("Token refresh failed");
        problem.Detail.Should().Be("Token has expired");
    }

    [Fact]
    public async Task RefreshToken_WhenFailedWithNoErrorInfo_UsesDefaultMessage()
    {
        RefreshTokenRequest request = new("some-token");
        TokenResult tokenResult = new(false, null, null, null, null, null, null, null, null);
        _tokenService.RefreshTokenAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(tokenResult);

        IActionResult result = await _controller.RefreshToken(request, CancellationToken.None);

        UnauthorizedObjectResult unauthorized = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        ProblemDetails problem = unauthorized.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Detail.Should().Be("Invalid or expired refresh token");
    }

    [Fact]
    public async Task RefreshToken_WhenSuccessWithNullOptionalFields_DefaultsCorrectly()
    {
        RefreshTokenRequest request = new("token");
        TokenResult tokenResult = new(true, "access", null, null, null, null, null, null, null);
        _tokenService.RefreshTokenAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(tokenResult);

        IActionResult result = await _controller.RefreshToken(request, CancellationToken.None);

        OkObjectResult ok = result.Should().BeOfType<OkObjectResult>().Subject;
        TokenResponse response = ok.Value.Should().BeOfType<TokenResponse>().Subject;
        response.TokenType.Should().Be("Bearer");
        response.ExpiresIn.Should().Be(300);
        response.RefreshToken.Should().BeNull();
        response.Scope.Should().BeNull();
    }

    [Fact]
    public async Task RefreshToken_PassesCorrectTokenToService()
    {
        RefreshTokenRequest request = new("my-refresh-token");
        TokenResult tokenResult = new(true, "token", null, "Bearer", 300, null, null, null, null);
        _tokenService.RefreshTokenAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(tokenResult);

        await _controller.RefreshToken(request, CancellationToken.None);

        await _tokenService.Received(1).RefreshTokenAsync("my-refresh-token", Arg.Any<CancellationToken>());
    }

    #endregion
}
