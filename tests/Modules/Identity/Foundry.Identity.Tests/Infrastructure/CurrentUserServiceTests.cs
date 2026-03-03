using System.Security.Claims;
using Foundry.Identity.Infrastructure.Services;
using Microsoft.AspNetCore.Http;

namespace Foundry.Identity.Tests.Infrastructure;

public class CurrentUserServiceTests
{
    [Fact]
    public void UserId_WhenAuthenticated_WithNameIdentifier_ReturnsGuid()
    {
        Guid userId = Guid.NewGuid();
        IHttpContextAccessor accessor = CreateAccessor(new Claim(ClaimTypes.NameIdentifier, userId.ToString()));

        CurrentUserService service = new CurrentUserService(accessor);

        service.UserId.Should().Be(userId);
    }

    [Fact]
    public void UserId_WhenAuthenticated_WithSubClaim_ReturnsGuid()
    {
        Guid userId = Guid.NewGuid();
        IHttpContextAccessor accessor = CreateAccessor(new Claim("sub", userId.ToString()));

        CurrentUserService service = new CurrentUserService(accessor);

        service.UserId.Should().Be(userId);
    }

    [Fact]
    public void UserId_WhenNotAuthenticated_ReturnsNull()
    {
        IHttpContextAccessor accessor = Substitute.For<IHttpContextAccessor>();
        DefaultHttpContext context = new DefaultHttpContext();
        // User is not authenticated by default
        accessor.HttpContext.Returns(context);

        CurrentUserService service = new CurrentUserService(accessor);

        service.UserId.Should().BeNull();
    }

    [Fact]
    public void UserId_WhenNoHttpContext_ReturnsNull()
    {
        IHttpContextAccessor accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns((HttpContext?)null);

        CurrentUserService service = new CurrentUserService(accessor);

        service.UserId.Should().BeNull();
    }

    [Fact]
    public void UserId_WhenClaimIsNotGuid_ReturnsNull()
    {
        IHttpContextAccessor accessor = CreateAccessor(new Claim(ClaimTypes.NameIdentifier, "not-a-guid"));

        CurrentUserService service = new CurrentUserService(accessor);

        service.UserId.Should().BeNull();
    }

    [Fact]
    public void UserId_WhenNoUserIdClaim_ReturnsNull()
    {
        IHttpContextAccessor accessor = CreateAccessor(new Claim("some-other-claim", "value"));

        CurrentUserService service = new CurrentUserService(accessor);

        service.UserId.Should().BeNull();
    }

    private static IHttpContextAccessor CreateAccessor(params Claim[] claims)
    {
        IHttpContextAccessor accessor = Substitute.For<IHttpContextAccessor>();
        DefaultHttpContext context = new DefaultHttpContext();
        ClaimsIdentity identity = new ClaimsIdentity(claims, "test");
        context.User = new ClaimsPrincipal(identity);
        accessor.HttpContext.Returns(context);
        return accessor;
    }
}
