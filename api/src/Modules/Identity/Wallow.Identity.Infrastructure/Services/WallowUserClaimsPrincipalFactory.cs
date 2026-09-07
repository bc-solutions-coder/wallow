using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Wallow.Identity.Domain.Entities;

namespace Wallow.Identity.Infrastructure.Services;

/// <summary>
/// Uses the user-only principal factory because organization roles are stored on
/// memberships, not ASP.NET Identity's user-role join. Organization-specific roles
/// are resolved during token issuance rather than through that unmapped join.
/// </summary>
public sealed class WallowUserClaimsPrincipalFactory(
    UserManager<WallowUser> userManager,
    IOptions<IdentityOptions> optionsAccessor)
    : UserClaimsPrincipalFactory<WallowUser>(userManager, optionsAccessor);
