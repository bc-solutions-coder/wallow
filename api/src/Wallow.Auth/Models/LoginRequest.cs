namespace Wallow.Auth.Models;

public sealed record LoginRequest(string Email, string Password, bool RememberMe);
