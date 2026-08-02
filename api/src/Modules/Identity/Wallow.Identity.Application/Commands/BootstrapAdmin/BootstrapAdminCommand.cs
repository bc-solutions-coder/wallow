namespace Wallow.Identity.Application.Commands.BootstrapAdmin;

/// <summary>
/// The first-run wizard's whole input. <paramref name="OrganizationName"/> is required because
/// roles are granted per organization: an administrator with no organization holds no permission
/// anywhere, so bootstrap has to know which organization it is creating.
/// </summary>
public sealed record BootstrapAdminCommand(
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string OrganizationName);

/// <summary>
/// Handles the low-level Identity operations needed for admin bootstrapping.
/// Implemented in Infrastructure using UserManager and RoleManager.
/// </summary>
public interface IBootstrapAdminService
{
    Task EnsureRoleExistsAsync(string roleName, CancellationToken ct = default);
    Task<Guid> CreateUserAsync(string email, string password, string firstName, string lastName, CancellationToken ct = default);

    /// <summary>
    /// Stamps the non-assignable global-administrator claim onto a seeded user. There is no
    /// counterpart on any tenant-facing surface: the claim is provisioned here or not at all.
    /// </summary>
    Task GrantGlobalAdminAsync(Guid userId, CancellationToken ct = default);
    Task<bool> UserExistsAsync(string email, CancellationToken ct = default);
}
