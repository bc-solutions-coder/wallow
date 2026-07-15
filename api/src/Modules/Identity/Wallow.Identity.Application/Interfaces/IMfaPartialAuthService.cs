namespace Wallow.Identity.Application.Interfaces;

public record MfaPartialAuthPayload(
    string UserId,
    string Email,
    string AuthMethod,
    bool RememberMe,
    DateTimeOffset IssuedAt);

public interface IMfaPartialAuthService
{
    Task IssuePartialCookieAsync(MfaPartialAuthPayload payload, CancellationToken ct);

    Task<MfaPartialAuthPayload?> ValidatePartialCookieAsync(CancellationToken ct);

    Task UpgradeToFullAuthAsync(string userId, bool isPersistent, CancellationToken ct);

    void DeletePartialCookie();
}
