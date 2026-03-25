namespace Wallow.Auth.Models;

public sealed record AuthResponse(
    bool Succeeded,
    string? Error = null,
    string? SignInTicket = null,
    bool MfaChallengeRequired = false,
    string? MfaChallengeToken = null,
    string? MfaMethod = null,
    bool MfaEnrollmentRequired = false,
    DateTimeOffset? MfaGraceDeadline = null);
