namespace Wallow.Identity.Application.Commands.BootstrapAdmin;

public sealed record BootstrapAdminCommand(
    string Email,
    string Password,
    string FirstName,
    string LastName);

/// <summary>
/// Handles the low-level Identity operations needed for admin bootstrapping.
/// Implemented in Infrastructure using UserManager and RoleManager.
/// </summary>
public interface IBootstrapAdminService
{
    Task EnsureRoleExistsAsync(string roleName, CancellationToken ct = default);
    Task<Guid> CreateUserAsync(string email, string password, string firstName, string lastName, CancellationToken ct = default);
    Task AssignRoleAsync(Guid userId, string roleName, CancellationToken ct = default);
    Task<bool> UserExistsAsync(string email, CancellationToken ct = default);
}
