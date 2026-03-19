namespace Foundry.Identity.Application.DTOs;

public record DeveloperAppRegistrationResult(
    string ClientId,
    string ClientSecret,
    string RegistrationAccessToken);
