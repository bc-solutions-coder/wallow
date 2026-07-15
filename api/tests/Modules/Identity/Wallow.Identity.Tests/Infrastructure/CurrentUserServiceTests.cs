using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Wallow.Shared.Infrastructure.Core.Services;

namespace Wallow.Identity.Tests.Infrastructure;

public class CurrentUserServiceTests
{
    [Fact]
    public void GetCurrentUserId_WhenAuthenticated_WithNameIdentifier_ReturnsGuid()
    {
        Guid userId = Guid.NewGuid();
        IHttpContextAccessor accessor = CreateAccessor(new Claim(ClaimTypes.NameIdentifier, userId.ToString()));

        CurrentUserService service = new(accessor);

        service.GetCurrentUserId().Should().Be(userId);
    }

    [Fact]
    public void GetCurrentUserId_WhenAuthenticated_WithSubClaim_ReturnsGuid()
    {
        Guid userId = Guid.NewGuid();
        IHttpContextAccessor accessor = CreateAccessor(new Claim("sub", userId.ToString()));

        CurrentUserService service = new(accessor);

        service.GetCurrentUserId().Should().Be(userId);
    }

    [Fact]
    public void GetCurrentUserId_WhenNotAuthenticated_ReturnsNull()
    {
        IHttpContextAccessor accessor = Substitute.For<IHttpContextAccessor>();
        DefaultHttpContext context = new DefaultHttpContext();
        // User is not authenticated by default
        accessor.HttpContext.Returns(context);

        CurrentUserService service = new(accessor);

        service.GetCurrentUserId().Should().BeNull();
    }

    [Fact]
    public void GetCurrentUserId_WhenNoHttpContext_ReturnsNull()
    {
        IHttpContextAccessor accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns((HttpContext?)null);

        CurrentUserService service = new(accessor);

        service.GetCurrentUserId().Should().BeNull();
    }

    [Fact]
    public void GetCurrentUserId_WhenClaimIsNotGuid_ReturnsNull()
    {
        IHttpContextAccessor accessor = CreateAccessor(new Claim(ClaimTypes.NameIdentifier, "not-a-guid"));

        CurrentUserService service = new(accessor);

        service.GetCurrentUserId().Should().BeNull();
    }

    [Fact]
    public void GetCurrentUserId_WhenNoUserIdClaim_ReturnsNull()
    {
        IHttpContextAccessor accessor = CreateAccessor(new Claim("some-other-claim", "value"));

        CurrentUserService service = new(accessor);

        service.GetCurrentUserId().Should().BeNull();
    }

    private static IHttpContextAccessor CreateAccessor(params Claim[] claims)
    {
        IHttpContextAccessor accessor = Substitute.For<IHttpContextAccessor>();
        DefaultHttpContext context = new DefaultHttpContext();
        ClaimsIdentity identity = new(claims, "test");
        context.User = new ClaimsPrincipal(identity);
        accessor.HttpContext.Returns(context);
        return accessor;
    }
}
