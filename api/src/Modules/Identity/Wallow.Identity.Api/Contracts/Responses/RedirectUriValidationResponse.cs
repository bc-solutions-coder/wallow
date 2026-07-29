namespace Wallow.Identity.Api.Contracts.Responses;

/// <summary>
/// Whether a candidate post-login redirect URI is registered for the requesting client.
/// </summary>
public sealed record RedirectUriValidationResponse(bool Allowed);
