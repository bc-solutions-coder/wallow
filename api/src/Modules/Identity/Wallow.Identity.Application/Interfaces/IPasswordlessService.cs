using Wallow.Shared.Kernel.Results;

namespace Wallow.Identity.Application.Interfaces;

/// <summary>
/// Magic-link and one-time-code sign-in. Sends succeed for unknown addresses too (no
/// enumeration); a throttled send fails with <c>RateLimit.Exceeded</c> carrying the wait.
/// Validation returns the address the credential was issued for.
/// </summary>
public interface IPasswordlessService
{
    Task<Result> SendMagicLinkAsync(string email, CancellationToken ct, string? returnUrl = null, string? clientId = null);

    Task<Result<string>> ValidateMagicLinkAsync(string token, CancellationToken ct);

    Task<Result> SendOtpAsync(string email, CancellationToken ct);

    Task<Result<string>> ValidateOtpAsync(string email, string code, CancellationToken ct);
}
