namespace Wallow.Identity.Api.Contracts.Requests;

public sealed record AccountRegisterRequest(string Email, string Password, string ConfirmPassword, string? ClientId = null, string? LoginMethod = null, string? ReturnUrl = null);
