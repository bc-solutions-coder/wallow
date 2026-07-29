namespace Wallow.Identity.Api.Contracts.Responses;

/// <summary>
/// A short-lived token the web app hands to the auth app's enrollment screen, which exchanges it
/// for the partial-auth cookie the enrollment endpoints authenticate against.
/// </summary>
public sealed record MfaEnrollmentTokenResponse(string Token);
