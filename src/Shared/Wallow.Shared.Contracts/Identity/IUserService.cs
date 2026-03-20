namespace Wallow.Shared.Contracts.Identity;

public interface IUserService
{
    Task<UserInfo?> GetUserByIdAsync(Guid userId, CancellationToken ct = default);
    Task<UserInfo?> GetUserByEmailAsync(string email, CancellationToken ct = default);
}

public record UserInfo(
    Guid Id,
    string Email,
    string? FirstName,
    string? LastName,
    bool IsActive);
