using System.Security.Claims;
using Hangfire;
using Hangfire.Dashboard;
using Microsoft.AspNetCore.Http;
using Wallow.Api.Middleware;
using Wallow.Shared.Kernel.Identity.Authorization;

namespace Wallow.Api.Tests.Middleware;

/// <summary>
/// Who reaches the Hangfire dashboard. The decision is a permission, not a role claim: roles are
/// granted per organization and the dashboard belongs to none, so the only claim that can answer
/// here is the one permission expansion has already minted from those roles.
/// </summary>
public class HangfireDashboardAuthFilterTests
{
    private readonly HangfireDashboardAuthFilter _sut = new(allowAnonymous: false);

    [Fact]
    public void Authorize_WhenAnonymousAccessIsConfigured_ReturnsTrueRegardlessOfAuth()
    {
        HangfireDashboardAuthFilter open = new(allowAnonymous: true);
        DashboardContext context = CreateDashboardContext(authenticated: false);

        bool result = open.Authorize(context);

        result.Should().BeTrue();
    }

    [Fact]
    public void Authorize_UnauthenticatedUser_ReturnsFalse()
    {
        DashboardContext context = CreateDashboardContext(authenticated: false);

        bool result = _sut.Authorize(context);

        result.Should().BeFalse();
    }

    [Fact]
    public void Authorize_AuthenticatedUserWithoutAdminAccess_ReturnsFalse()
    {
        DashboardContext context = CreateDashboardContext(
            authenticated: true, permission: PermissionType.UsersRead);

        bool result = _sut.Authorize(context);

        result.Should().BeFalse();
    }

    [Fact]
    public void Authorize_AuthenticatedUserWithAdminAccess_ReturnsTrue()
    {
        DashboardContext context = CreateDashboardContext(
            authenticated: true, permission: PermissionType.AdminAccess);

        bool result = _sut.Authorize(context);

        result.Should().BeTrue();
    }

    [Fact]
    public void Authorize_AuthenticatedUserCarryingOnlyAnAdminRoleClaim_ReturnsFalse()
    {
        DefaultHttpContext httpContext = new()
        {
            RequestServices = Substitute.For<IServiceProvider>(),
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.Name, "test-user"), new Claim(ClaimTypes.Role, "admin")],
                "TestAuth")),
        };

        bool result = _sut.Authorize(CreateDashboardContext(httpContext));

        result.Should().BeFalse();
    }

    private static AspNetCoreDashboardContext CreateDashboardContext(
        bool authenticated, string? permission = null)
    {
        DefaultHttpContext httpContext = new()
        {
            RequestServices = Substitute.For<IServiceProvider>(),
        };

        if (authenticated)
        {
            List<Claim> claims = [new Claim(ClaimTypes.Name, "test-user")];
            if (permission is not null)
            {
                claims.Add(new Claim("permission", permission));
            }

            httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
        }

        return CreateDashboardContext(httpContext);
    }

    private static AspNetCoreDashboardContext CreateDashboardContext(HttpContext httpContext)
    {
        JobStorage storage = Substitute.For<JobStorage>();
        DashboardOptions options = new();
        return new AspNetCoreDashboardContext(storage, options, httpContext);
    }
}
