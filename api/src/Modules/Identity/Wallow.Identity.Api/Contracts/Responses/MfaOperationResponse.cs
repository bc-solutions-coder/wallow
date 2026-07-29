namespace Wallow.Identity.Api.Contracts.Responses;

/// <summary>
/// Success envelope for the MFA endpoints that report only whether the operation was carried out.
/// </summary>
public sealed record MfaOperationResponse(bool Succeeded);
