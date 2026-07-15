using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Wallow.Shared.Infrastructure.Core.Services;
using Wallow.Shared.Kernel.Services;

namespace Wallow.Shared.Infrastructure.Tests.Services;

public class CurrentUserServiceTests
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly CurrentUserService _sut;

    public CurrentUserServiceTests()
    {
        _httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        _sut = new CurrentUserService(_httpContextAccessor);
    }

    [Fact]
    public void GetCurrentUserId_WhenNoHttpContext_ReturnsNull()
    {
        _httpContextAccessor.HttpContext.Returns((HttpContext?)null);

        Guid? result = _sut.GetCurrentUserId();

        result.Should().BeNull();
    }

    [Fact]
    public void GetCurrentUserId_WhenUserNotAuthenticated_ReturnsNull()
    {
        DefaultHttpContext context = new();
        ClaimsPrincipal unauthenticatedUser = new(new ClaimsIdentity());
        context.User = unauthenticatedUser;
        _httpContextAccessor.HttpContext.Returns(context);

        Guid? result = _sut.GetCurrentUserId();

        result.Should().BeNull();
    }

    [Fact]
    public void GetCurrentUserId_WhenAuthenticatedWithNameIdentifierClaim_ReturnsUserId()
    {
        Guid userId = Guid.NewGuid();
        DefaultHttpContext context = new();
        ClaimsIdentity identity = new([
            new Claim(ClaimTypes.NameIdentifier, userId.ToString())
        ], "TestAuth");
        context.User = new ClaimsPrincipal(identity);
        _httpContextAccessor.HttpContext.Returns(context);

        Guid? result = _sut.GetCurrentUserId();

        result.Should().Be(userId);
    }

    [Fact]
    public void GetCurrentUserId_WhenAuthenticatedWithSubClaim_ReturnsUserId()
    {
        Guid userId = Guid.NewGuid();
        DefaultHttpContext context = new();
        ClaimsIdentity identity = new([
            new Claim("sub", userId.ToString())
        ], "TestAuth");
        context.User = new ClaimsPrincipal(identity);
        _httpContextAccessor.HttpContext.Returns(context);

        Guid? result = _sut.GetCurrentUserId();

        result.Should().Be(userId);
    }

    [Fact]
    public void GetCurrentUserId_WhenAuthenticatedButNoUserIdClaim_ReturnsNull()
    {
        DefaultHttpContext context = new();
        ClaimsIdentity identity = new([
            new Claim(ClaimTypes.Email, "user@example.com")
        ], "TestAuth");
        context.User = new ClaimsPrincipal(identity);
        _httpContextAccessor.HttpContext.Returns(context);

        Guid? result = _sut.GetCurrentUserId();

        result.Should().BeNull();
    }

    [Fact]
    public void GetCurrentUserId_WhenUserIdClaimIsNotGuid_ReturnsNull()
    {
        DefaultHttpContext context = new();
        ClaimsIdentity identity = new([
            new Claim(ClaimTypes.NameIdentifier, "not-a-guid")
        ], "TestAuth");
        context.User = new ClaimsPrincipal(identity);
        _httpContextAccessor.HttpContext.Returns(context);

        Guid? result = _sut.GetCurrentUserId();

        result.Should().BeNull();
    }

    [Fact]
    public void GetCurrentUserId_PrefersNameIdentifierOverSub()
    {
        Guid nameIdentifierGuid = Guid.NewGuid();
        Guid subGuid = Guid.NewGuid();
        DefaultHttpContext context = new();
        ClaimsIdentity identity = new([
            new Claim(ClaimTypes.NameIdentifier, nameIdentifierGuid.ToString()),
            new Claim("sub", subGuid.ToString())
        ], "TestAuth");
        context.User = new ClaimsPrincipal(identity);
        _httpContextAccessor.HttpContext.Returns(context);

        Guid? result = _sut.GetCurrentUserId();

        result.Should().Be(nameIdentifierGuid);
    }
}

public class CurrentUserServiceExtensionsTests
{
    [Fact]
    public void AddCurrentUserService_RegistersICurrentUserServiceAsScoped()
    {
        ServiceCollection services = new();
        services.AddHttpContextAccessor();

        services.AddCurrentUserService();

        ServiceProvider provider = services.BuildServiceProvider();
        ICurrentUserService? service = provider.GetService<ICurrentUserService>();
        service.Should().NotBeNull();
    }

    [Fact]
    public void AddCurrentUserService_ReturnsServiceCollectionForChaining()
    {
        ServiceCollection services = new();
        services.AddHttpContextAccessor();

        IServiceCollection result = services.AddCurrentUserService();

        result.Should().BeSameAs(services);
    }
}
