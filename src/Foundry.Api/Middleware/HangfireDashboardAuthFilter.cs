using System.Security.Claims;
using Hangfire.Dashboard;

namespace Foundry.Api.Middleware;

internal sealed class HangfireDashboardAuthFilter : IDashboardAuthorizationFilter
{
    private readonly IWebHostEnvironment _environment;

    public HangfireDashboardAuthFilter(IWebHostEnvironment environment)
    {
        _environment = environment;
    }

    public bool Authorize(DashboardContext context)
    {
        if (_environment.IsDevelopment())
        {
            return true;
        }

        HttpContext httpContext = context.GetHttpContext();
        return httpContext.User.Identity?.IsAuthenticated == true
            && httpContext.User.Claims.Any(c =>
                c.Type == ClaimTypes.Role
                && c.Value.Equals("admin", StringComparison.OrdinalIgnoreCase));
    }
}
