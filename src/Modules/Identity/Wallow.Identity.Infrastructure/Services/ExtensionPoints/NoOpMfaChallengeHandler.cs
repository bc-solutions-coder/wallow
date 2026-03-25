using System.Diagnostics.CodeAnalysis;

using Wallow.Identity.Application.Interfaces;
using Wallow.Shared.Kernel.Results;

namespace Wallow.Identity.Infrastructure.Services.ExtensionPoints;

[ExcludeFromCodeCoverage]
internal sealed class NoOpMfaChallengeHandler : IMfaChallengeHandler
{
    public Task<bool> ShouldChallengeAsync(string userId, CancellationToken ct)
    {
        return Task.FromResult(false);
    }

    public Task<Result> ValidateAsync(string userId, string code, CancellationToken ct)
    {
        return Task.FromResult(Result.Success());
    }
}
