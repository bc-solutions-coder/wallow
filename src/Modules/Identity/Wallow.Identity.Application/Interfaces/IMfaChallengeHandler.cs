using Wallow.Shared.Kernel.Results;

namespace Wallow.Identity.Application.Interfaces;

public interface IMfaChallengeHandler
{
    Task<bool> ShouldChallengeAsync(string userId, CancellationToken ct);

    Task<Result> ValidateAsync(string userId, string code, CancellationToken ct);
}
