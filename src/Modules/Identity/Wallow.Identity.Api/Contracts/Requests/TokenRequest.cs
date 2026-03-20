namespace Wallow.Identity.Api.Contracts.Requests;

/// <summary>
/// Request for obtaining an access token via username/password.
/// </summary>
public sealed record TokenRequest(
    string Email,
    string Password);
