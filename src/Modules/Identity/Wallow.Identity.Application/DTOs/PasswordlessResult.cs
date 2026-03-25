namespace Wallow.Identity.Application.DTOs;

public record PasswordlessResult(
    bool Succeeded,
    string? Email,
    string? Error);
