using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Filters;
using Wallow.Identity.Application.DTOs;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Errors;
using Wallow.Shared.Kernel.Domain;
using Wallow.Shared.Kernel.Extensions;

namespace Wallow.Identity.Api.Authorization;

/// <summary>
/// While a platform suspension stands on an organization, every change to it is refused. Reads
/// stay open, because the organization's admins are meant to see the reason; global admins pass,
/// because the operator who placed the freeze is the one who lifts it. Applied to the org-scoped
/// controllers via <c>TypeFilter</c>, it finds the organization in the route and answers with the
/// same business-rule refusal the domain gives, so callers see one shape of "suspended".
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
