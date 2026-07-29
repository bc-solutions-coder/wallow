namespace Wallow.Identity.Api.Contracts.Responses;

/// <summary>
/// Success envelope returned by the cookie-auth account endpoints that report only whether the
/// operation was carried out (registration, password reset, e-mail verification, magic link and
/// OTP dispatch).
/// </summary>
public sealed record AccountOperationResponse(bool Succeeded);
