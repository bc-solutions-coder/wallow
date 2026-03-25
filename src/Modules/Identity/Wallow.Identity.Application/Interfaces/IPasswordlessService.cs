using Wallow.Identity.Application.DTOs;

namespace Wallow.Identity.Application.Interfaces;

public interface IPasswordlessService
{
    Task<PasswordlessResult> SendMagicLinkAsync(string email, CancellationToken ct);

    Task<PasswordlessResult> ValidateMagicLinkAsync(string token, CancellationToken ct);

    Task<PasswordlessResult> SendOtpAsync(string email, CancellationToken ct);

    Task<PasswordlessResult> ValidateOtpAsync(string email, string code, CancellationToken ct);
}
