using Wallow.Identity.Application.DTOs;

namespace Wallow.Identity.Application.Interfaces;

public interface IMfaLockoutService
{
    Task<MfaLockoutResult> RecordFailureAsync(Guid userId, int maxAttempts, CancellationToken ct);
    Task ResetAsync(Guid userId, CancellationToken ct);
}
