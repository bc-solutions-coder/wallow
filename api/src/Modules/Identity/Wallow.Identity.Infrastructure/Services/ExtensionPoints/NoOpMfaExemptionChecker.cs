using System.Diagnostics.CodeAnalysis;

using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Entities;

namespace Wallow.Identity.Infrastructure.Services.ExtensionPoints;

[ExcludeFromCodeCoverage]
internal sealed class NoOpMfaExemptionChecker : IMfaExemptionChecker
{
    public Task<bool> IsExemptAsync(WallowUser user, CancellationToken ct)
    {
        return Task.FromResult(false);
    }
}
