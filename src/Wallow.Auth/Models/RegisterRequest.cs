namespace Wallow.Auth.Models;

public sealed record RegisterRequest(string Email, string Password, string ConfirmPassword, string? ClientId = null, string? LoginMethod = null);
