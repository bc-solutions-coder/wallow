using Wallow.Identity.Api.Contracts.Requests;
using Wallow.Identity.Api.Controllers;
using Wallow.Identity.Application.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Wallow.Identity.Tests.Api.Controllers;

public class AuthControllerLogoutTests
{
    private readonly ITokenService _tokenService;
    private readonly AuthController _controller;

    public AuthControllerLogoutTests()
    {
        _tokenService = Substitute.For<ITokenService>();
        _controller = new AuthController(_tokenService, Substitute.For<ILogger<AuthController>>());
    }

    [Fact]
    public async Task Logout_WithValidRefreshToken_ReturnsNoContent()
    {
        LogoutRequest request = new("valid-refresh-token");
        _tokenService.RevokeTokenAsync("valid-refresh-token", Arg.Any<CancellationToken>())
            .Returns(true);

        IActionResult result = await _controller.Logout(request, CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
        await _tokenService.Received(1).RevokeTokenAsync("valid-refresh-token", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Logout_WithEmptyRefreshToken_ReturnsBadRequest()
    {
        LogoutRequest request = new("");

        IActionResult result = await _controller.Logout(request, CancellationToken.None);

        BadRequestObjectResult badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        ProblemDetails problem = badRequest.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Detail.Should().Be("Refresh token is required");
        problem.Status.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task Logout_WithWhitespaceToken_ReturnsBadRequest()
    {
        LogoutRequest request = new("   ");

        IActionResult result = await _controller.Logout(request, CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Logout_WhenRevocationFails_ReturnsBadRequest()
    {
        LogoutRequest request = new("bad-token");
        _tokenService.RevokeTokenAsync("bad-token", Arg.Any<CancellationToken>())
            .Returns(false);

        IActionResult result = await _controller.Logout(request, CancellationToken.None);

        BadRequestObjectResult badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        ProblemDetails problem = badRequest.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Title.Should().Be("Logout failed");
        problem.Detail.Should().Be("Failed to revoke the token");
    }

    [Fact]
    public async Task Logout_PassesTokenToService()
    {
        LogoutRequest request = new("my-token");
        _tokenService.RevokeTokenAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(true);

        await _controller.Logout(request, CancellationToken.None);

        await _tokenService.Received(1).RevokeTokenAsync("my-token", Arg.Any<CancellationToken>());
    }
}
