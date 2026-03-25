using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Wallow.Shared.Kernel.Extensions;
using Wallow.Shared.Kernel.Services;

namespace Wallow.Shared.Infrastructure.Core.Services;

public sealed class CurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUserService
{
    public Guid? GetCurrentUserId()
    {
        ClaimsPrincipal? user = httpContextAccessor.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        string? userIdClaim = user.GetUserId();

        if (userIdClaim is not null && Guid.TryParse(userIdClaim, out Guid userId))
        {
            return userId;
        }

        return null;
    }
}

public static class CurrentUserServiceExtensions
{
    public static IServiceCollection AddCurrentUserService(this IServiceCollection services)
    {
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        return services;
    }
}
