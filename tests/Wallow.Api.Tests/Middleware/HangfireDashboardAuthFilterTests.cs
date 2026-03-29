using System.Security.Claims;
using Hangfire;
using Hangfire.Dashboard;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Wallow.Api.Middleware;

namespace Wallow.Api.Tests.Middleware;

public class HangfireDashboardAuthFilterTests
{
    private readonly IWebHostEnvironment _environment = Substitute.For<IWebHostEnvironment>();
    private readonly HangfireDashboardAuthFilter _sut;

    public HangfireDashboardAuthFilterTests()
    {
        _environment.EnvironmentName.Returns("Production");
        _sut = new HangfireDashboardAuthFilter(_environment);
    }

    [Fact]
    public void Authorize_InDevelopmentEnvironment_ReturnsTrueRegardlessOfAuth()
    {
        _environment.EnvironmentName.Returns("Development");
        HangfireDashboardAuthFilter devFilter = new(_environment);
        DashboardContext context = CreateDashboardContext(authenticated: false);

        bool result = devFilter.Authorize(context);

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
    public void Authorize_AuthenticatedNonAdminUser_ReturnsFalse()
    {
        DashboardContext context = CreateDashboardContext(authenticated: true, role: "User");

        bool result = _sut.Authorize(context);

        result.Should().BeFalse();
    }

    [Fact]
    public void Authorize_AuthenticatedAdminUser_ReturnsTrue()
    {
        DashboardContext context = CreateDashboardContext(authenticated: true, role: "Admin");

        bool result = _sut.Authorize(context);

        result.Should().BeTrue();
    }

    private static AspNetCoreDashboardContext CreateDashboardContext(bool authenticated, string? role = null)
    {
        DefaultHttpContext httpContext = new();
        httpContext.RequestServices = Substitute.For<IServiceProvider>();

        if (authenticated)
        {
            List<Claim> claims = [new Claim(ClaimTypes.Name, "test-user")];
            if (role is not null)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }
            ClaimsIdentity identity = new(claims, "TestAuth");
            httpContext.User = new ClaimsPrincipal(identity);
        }

        JobStorage storage = Substitute.For<JobStorage>();
        DashboardOptions options = new();
        return new AspNetCoreDashboardContext(storage, options, httpContext);
    }
}
