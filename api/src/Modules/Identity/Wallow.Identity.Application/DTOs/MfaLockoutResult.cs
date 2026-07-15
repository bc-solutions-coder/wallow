namespace Wallow.Identity.Application.DTOs;

public record MfaLockoutResult(
    bool IsLockedOut,
    int FailedAttempts,
    int LockoutCount,
    DateTimeOffset? LockoutEnd);
