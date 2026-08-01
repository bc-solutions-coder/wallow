using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Wallow.Identity.Domain.Entities;

namespace Wallow.Identity.Infrastructure.Services;

/// <summary>
/// Shapes the principal behind the auth cookie: it carries an org_id so cookie-based
/// authentication (the exchange-ticket flow among them) resolves a tenant at all, and it carries
/// no role claims, because a role is granted by an organization and the cookie names only one.
/// Roles are resolved per organization at token issuance instead.
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

        // A role is granted by an organization and means nothing outside it. The cookie names no
        // organization, so the role claims the base factory stamps from AspNetUserRoles would be
        // authority with no scope — and they ride the exchange-ticket flow into token issuance.
        // Roles are resolved per organization when a token is issued, never carried in here.
        foreach (Claim roleClaim in identity.FindAll(identity.RoleClaimType).ToList())
        {
            identity.RemoveClaim(roleClaim);
        }

        if (user.TenantId != Guid.Empty)
        {
            identity.AddClaim(new Claim("org_id", user.TenantId.ToString()));
        }

        return identity;
    }
}
