using System.Security.Claims;

namespace Wallow.Identity.Application.Interfaces;

public interface IExternalClaimsMapper
{
    Task<IDictionary<string, string>> MapAsync(string provider, IEnumerable<Claim> claims, CancellationToken ct);
}
