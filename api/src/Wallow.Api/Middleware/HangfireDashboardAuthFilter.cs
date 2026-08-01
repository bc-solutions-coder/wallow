using Hangfire.Dashboard;
using Wallow.Shared.Kernel.Extensions;
using Wallow.Shared.Kernel.Identity.Authorization;

namespace Wallow.Api.Middleware;

/// <summary>
/// Guards the Hangfire dashboard on the same permission every other administrative surface uses.
/// It reads permissions rather than role claims because a role is granted by an organization and
/// the dashboard belongs to none: PermissionExpansionMiddleware has already turned whatever roles
/// the caller's organization granted into permissions by the time this runs.
/// </summary>
internal sealed class HangfireDashboardAuthFilter(bool allowAnonymous) : IDashboardAuthorizationFilter
{
    /// <summary>
    /// Opens the dashboard to anyone who can reach it. Deliberately a configuration flag rather
    /// than an environment check: a deployment that turns this on has said so, and no environment
    /// name can turn it on by accident.
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
