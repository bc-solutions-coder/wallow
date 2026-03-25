using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;

using Wallow.Identity.Application.Interfaces;

namespace Wallow.Identity.Infrastructure.Services.ExtensionPoints;

[ExcludeFromCodeCoverage]
internal sealed class NoOpExternalClaimsMapper : IExternalClaimsMapper
{
    public Task<IDictionary<string, string>> MapAsync(string provider, IEnumerable<Claim> claims, CancellationToken ct)
    {
        IDictionary<string, string> empty = new Dictionary<string, string>();
        return Task.FromResult(empty);
    }
}
