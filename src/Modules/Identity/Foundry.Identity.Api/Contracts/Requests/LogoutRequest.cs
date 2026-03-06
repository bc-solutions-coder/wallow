namespace Foundry.Identity.Api.Contracts.Requests;

public sealed record LogoutRequest(
    string RefreshToken);
