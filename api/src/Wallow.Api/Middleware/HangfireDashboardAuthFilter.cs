using System.Security.Claims;
using Hangfire.Dashboard;

namespace Wallow.Api.Middleware;

internal sealed class HangfireDashboardAuthFilter(IWebHostEnvironment environment) : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        if (environment.IsDevelopment())
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
