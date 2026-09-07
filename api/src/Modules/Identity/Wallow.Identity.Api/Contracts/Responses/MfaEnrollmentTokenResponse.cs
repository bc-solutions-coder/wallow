namespace Wallow.Identity.Api.Contracts.Responses;

/// <summary>
/// Short-lived token exchangeable for the partial-auth cookie used during MFA enrollment.
/// </summary>
public sealed record MfaEnrollmentTokenResponse(string Token);
