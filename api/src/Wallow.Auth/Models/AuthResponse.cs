namespace Wallow.Auth.Models;

public sealed record AuthResponse(
    bool Succeeded,
    string? Error = null,
    bool MfaRequired = false,
    bool MfaEnrollmentRequired = false,
    DateTimeOffset? MfaGraceDeadline = null,
    string? SignInTicket = null);
