namespace Wallow.Identity.Api.Contracts.Responses;

public record AppRegistrationResponse(string ClientId, string ClientSecret, string RegistrationAccessToken);
