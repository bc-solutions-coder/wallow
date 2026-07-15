using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Wallow.Identity.Domain.Entities;

namespace Wallow.Identity.Infrastructure.Services;

/// <summary>
/// Extends the default Identity claims principal to include org_id and org_name claims.
/// Without this, cookie-based authentication (e.g. exchange-ticket flow) would lack tenant
/// context, causing TenantResolutionMiddleware to leave ITenantContext unset and any
/// IsCurrentTenantOrg check to fail with 404.
/// </summary>
public sealed class WallowUserClaimsPrincipalFactory(
    UserManager<WallowUser> userManager,
    RoleManager<WallowRole> roleManager,
    IOptions<IdentityOptions> optionsAccessor)
    : UserClaimsPrincipalFactory<WallowUser, WallowRole>(userManager, roleManager, optionsAccessor)
{
    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(WallowUser user)
    {
        ClaimsIdentity identity = await base.GenerateClaimsAsync(user);

        if (user.TenantId != Guid.Empty)
        {
            identity.AddClaim(new Claim("org_id", user.TenantId.ToString()));
        }

        return identity;
    }
}
