using System.Security.Claims;

using Wallow.Identity.Domain.Entities;

namespace Wallow.Identity.Application.Interfaces;

public interface IClaimsEnricher
{
    Task<ClaimsPrincipal> EnrichAsync(ClaimsPrincipal principal, WallowUser user, CancellationToken ct);
}
