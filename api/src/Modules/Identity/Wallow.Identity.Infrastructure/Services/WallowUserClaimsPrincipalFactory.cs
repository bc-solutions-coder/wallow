using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Wallow.Identity.Domain.Entities;

namespace Wallow.Identity.Infrastructure.Services;

/// <summary>
/// Shapes the principal behind the auth cookie. The cookie names a person and nothing else: no
/// role claims and no org_id, because a person belongs to many organizations and the cookie can
/// name none of them. Both are resolved per organization when a token is issued, from the
/// organization the OIDC client is bound to.
/// </summary>
/// <remarks>
/// A cookie-authenticated request therefore resolves no tenant, and the endpoints it can still
/// reach are the ones that need none — sign-in, the authorize handshake, MFA step-up, logout and
/// account self-service. Everything tenant-scoped is permission-gated, and permissions come from
/// roles the cookie does not carry. A tenant-scoped row cannot be written without one either:
/// <see cref="Wallow.Shared.Kernel.MultiTenancy.TenantScope.Require"/> refuses at construction.
/// </remarks>
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

        return identity;
    }
}
