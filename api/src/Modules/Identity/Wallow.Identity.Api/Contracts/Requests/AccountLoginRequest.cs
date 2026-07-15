namespace Wallow.Identity.Api.Contracts.Requests;

public sealed record AccountLoginRequest(string Email, string Password, bool RememberMe);
