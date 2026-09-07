namespace Wallow.Identity.Api.Contracts.Responses;

/// <summary>
/// Whether the redirect URI has an allowed origin for the supplied client context.
/// </summary>
public sealed record RedirectUriValidationResponse(bool Allowed);
