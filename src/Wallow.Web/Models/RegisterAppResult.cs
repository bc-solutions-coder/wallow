namespace Wallow.Web.Models;

public sealed record RegisterAppResult(
    string? ClientId,
    string? ClientSecret,
    string? RegistrationAccessToken,
    bool Success,
    string? ErrorMessage);
