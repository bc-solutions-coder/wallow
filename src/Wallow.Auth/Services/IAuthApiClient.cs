using Wallow.Auth.Models;

namespace Wallow.Auth.Services;

public interface IAuthApiClient
{
    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default);
    Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default);
    Task<AuthResponse> ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken ct = default);
    Task<AuthResponse> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct = default);
    Task<AuthResponse> VerifyEmailAsync(string email, string token, CancellationToken ct = default);
    Task<bool> ValidateRedirectUriAsync(string uri, CancellationToken ct = default);
    Task<List<string>> GetExternalProvidersAsync(CancellationToken ct = default);
    Task<string?> GetMatchingOrganizationByDomainAsync(string email, CancellationToken ct = default);
    Task<bool> RequestMembershipAsync(string emailDomain, CancellationToken ct = default);
    Task<AuthResponse> SendMagicLinkAsync(string email, CancellationToken ct = default);
    Task<AuthResponse> VerifyMagicLinkAsync(string token, CancellationToken ct = default);
    Task<AuthResponse> SendOtpAsync(string email, CancellationToken ct = default);
    Task<AuthResponse> VerifyOtpAsync(string email, string code, CancellationToken ct = default);
    Task<AuthResponse> VerifyMfaChallengeAsync(string challengeToken, string code, CancellationToken ct = default);
    Task<AuthResponse> UseBackupCodeAsync(string challengeToken, string code, CancellationToken ct = default);
    Task<InvitationDetailsResponse?> VerifyInvitationAsync(string token, CancellationToken ct = default);
    Task<bool> AcceptInvitationAsync(string token, CancellationToken ct = default);
}

public record InvitationDetailsResponse(
    Guid Id,
    string Email,
    string Status,
    DateTimeOffset ExpiresAt,
    DateTimeOffset CreatedAt,
    Guid? AcceptedByUserId);
