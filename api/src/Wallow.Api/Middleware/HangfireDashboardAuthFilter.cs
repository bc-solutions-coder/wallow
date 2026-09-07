using Hangfire.Dashboard;
using Wallow.Shared.Kernel.Extensions;
using Wallow.Shared.Kernel.Identity.Authorization;

namespace Wallow.Api.Middleware;

/// <summary>
/// Requires authenticated AdminAccess permission unless anonymous dashboard access is enabled.
/// </summary>
internal sealed class HangfireDashboardAuthFilter(bool allowAnonymous) : IDashboardAuthorizationFilter
{
    /// <summary>
    /// Configuration opt-in for anonymous dashboard access.
    /// </summary>
    public const string AllowAnonymousConfigurationKey = "Hangfire:AllowAnonymousDashboard";

    public bool Authorize(DashboardContext context)
    {
        if (allowAnonymous)
        {
            return true;
        }

        HttpContext httpContext = context.GetHttpContext();
        return httpContext.User.Identity?.IsAuthenticated == true
            && httpContext.User.GetPermissions().Contains(
                PermissionType.AdminAccess, StringComparer.OrdinalIgnoreCase);
    }
}
