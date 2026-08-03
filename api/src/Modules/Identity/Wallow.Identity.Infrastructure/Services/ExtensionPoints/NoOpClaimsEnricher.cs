using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;

using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Entities;

namespace Wallow.Identity.Infrastructure.Services.ExtensionPoints;

[ExcludeFromCodeCoverage]
public sealed class NoOpClaimsEnricher : IClaimsEnricher
{
    public Task<ClaimsPrincipal> EnrichAsync(ClaimsPrincipal principal, WallowUser user, CancellationToken ct)
    {
        return Task.FromResult(principal);
    }
}
