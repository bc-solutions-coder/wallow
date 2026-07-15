using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Infrastructure.Authorization;

namespace Wallow.Identity.Tests.Infrastructure;

public class MfaPartialAuthorizationHandlerTests
{
    private readonly IMfaPartialAuthService _mfaPartialAuthService = Substitute.For<IMfaPartialAuthService>();
    private readonly MfaPartialAuthorizationHandler _handler;
    private readonly MfaPartialRequirement _requirement = new();

    public MfaPartialAuthorizationHandlerTests()
    {
        _handler = new MfaPartialAuthorizationHandler(_mfaPartialAuthService);
    }

    [Fact]
    public async Task HandleRequirementAsync_AuthenticatedUser_Succeeds()
    {
        ClaimsPrincipal user = new(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())],
            "test"));

        AuthorizationHandlerContext context = new(
            [_requirement], user, null);

        await _handler.HandleAsync(context);

        context.HasSucceeded.Should().BeTrue();
    }

    [Fact]
    public async Task HandleRequirementAsync_UnauthenticatedWithValidPartialCookie_Succeeds()
    {
        ClaimsPrincipal user = new(new ClaimsIdentity());

        MfaPartialAuthPayload payload = new(
            UserId: Guid.NewGuid().ToString(),
            Email: "test@example.com",
            AuthMethod: "Password",
            RememberMe: false,
            IssuedAt: DateTimeOffset.UtcNow);

        _mfaPartialAuthService
            .ValidatePartialCookieAsync(Arg.Any<CancellationToken>())
            .Returns(payload);

        AuthorizationHandlerContext context = new(
            [_requirement], user, null);

        await _handler.HandleAsync(context);

        context.HasSucceeded.Should().BeTrue();
    }

    [Fact]
    public async Task HandleRequirementAsync_UnauthenticatedWithNoPartialCookie_DoesNotSucceed()
    {
        ClaimsPrincipal user = new(new ClaimsIdentity());

        _mfaPartialAuthService
            .ValidatePartialCookieAsync(Arg.Any<CancellationToken>())
            .Returns((MfaPartialAuthPayload?)null);

        AuthorizationHandlerContext context = new(
            [_requirement], user, null);

        await _handler.HandleAsync(context);

        context.HasSucceeded.Should().BeFalse();
    }
}
