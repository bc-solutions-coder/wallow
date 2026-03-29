using Wallow.Identity.Domain.Entities;

namespace Wallow.Identity.Application.Interfaces;

public interface IMfaExemptionChecker
{
    Task<bool> IsExemptAsync(WallowUser user, CancellationToken ct);
}
