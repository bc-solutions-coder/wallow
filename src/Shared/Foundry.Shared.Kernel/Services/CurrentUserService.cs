using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace Foundry.Shared.Kernel.Services;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid? GetCurrentUserId()
    {
        string? userIdClaim = _httpContextAccessor.HttpContext?.User
            .FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? _httpContextAccessor.HttpContext?.User
            .FindFirst("sub")?.Value;

        if (userIdClaim is not null && Guid.TryParse(userIdClaim, out Guid userId))
        {
            return userId;
        }

        return null;
    }
}
