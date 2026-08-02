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
/// <para>
/// It derives from the USER-ONLY factory on purpose. The role-aware
/// <c>UserClaimsPrincipalFactory&lt;TUser, TRole&gt;</c> stamps role claims from ASP.NET Identity's
/// user-role join — a directory this schema does not have, because roles hang off a membership of
/// one organization. Stripping those claims after the fact worked only while the join still
/// existed; not asking for them is the version that survives its removal.
/// </para>
/// <para>
/// A cookie-authenticated request therefore resolves no tenant, and the endpoints it can still
/// reach are the ones that need none — sign-in, the authorize handshake, MFA step-up, logout and
/// account self-service. Everything tenant-scoped is permission-gated, and permissions come from
/// roles the cookie does not carry. A tenant-scoped row cannot be written without one either:
/// <see cref="Wallow.Shared.Kernel.MultiTenancy.TenantScope.Require"/> refuses at construction.
/// </para>
/// </remarks>
public sealed class WallowUserClaimsPrincipalFactory(
    UserManager<WallowUser> userManager,
    IOptions<IdentityOptions> optionsAccessor)
    : UserClaimsPrincipalFactory<WallowUser>(userManager, optionsAccessor);
