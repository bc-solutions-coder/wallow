using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Filters;
using Wallow.Identity.Application.DTOs;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Errors;
using Wallow.Shared.Kernel.Domain;
using Wallow.Shared.Kernel.Extensions;

namespace Wallow.Identity.Api.Authorization;

/// <summary>
/// Rejects mutations to a platform-suspended organization on decorated controllers.
/// GET, HEAD, OPTIONS, global administrators, and routes without an organization id pass through.
/// </summary>
public sealed class RefusePlatformSuspendedOrganizationFilter(IOrganizationService organizations) : IAsyncActionFilter
{
    private static readonly string[] _organizationRouteKeys = ["id", "orgId"];

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        string method = context.HttpContext.Request.Method;
        if (HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method)
            || context.HttpContext.User.IsGlobalAdmin()
            || OrganizationIdOf(context) is not Guid organizationId)
        {
            await next();
            return;
        }

        OrganizationDto? organization = await organizations.GetOrganizationByIdAsync(
            organizationId, context.HttpContext.RequestAborted);
        if (organization?.PlatformSuspendedAt is not null)
        {
            throw new BusinessRuleException(IdentityErrors.OrganizationSuspendedByPlatform);
        }

        await next();
    }

    private static Guid? OrganizationIdOf(ActionExecutingContext context)
    {
        foreach (string key in _organizationRouteKeys)
        {
            if (context.RouteData.Values.TryGetValue(key, out object? value)
                && value is string raw
                && Guid.TryParse(raw, out Guid organizationId))
            {
                return organizationId;
            }
        }

        return null;
    }
}
