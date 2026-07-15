namespace Wallow.Identity.Api.Contracts.Requests;

public sealed record AccountResetPasswordRequest(string Email, string Token, string NewPassword);
