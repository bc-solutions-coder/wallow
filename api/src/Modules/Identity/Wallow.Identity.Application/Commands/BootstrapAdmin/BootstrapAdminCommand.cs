namespace Wallow.Identity.Application.Commands.BootstrapAdmin;

/// <summary>
/// First-run administrator details and the organization in which to grant the admin role.
/// </summary>
public sealed record BootstrapAdminCommand(
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string OrganizationName);

/// <summary>
/// User and role operations used by bootstrap and seeding.
/// </summary>
public interface IBootstrapAdminService
{
    Task EnsureRoleExistsAsync(string roleName, CancellationToken ct = default);
    Task<Guid> CreateUserAsync(string email, string password, string firstName, string lastName, CancellationToken ct = default);

    /// <summary>
    /// Provisions a seeded user with the global-administrator claim.
    /// Tenant-facing endpoints do not expose this operation.
    /// </summary>
    Task GrantGlobalAdminAsync(Guid userId, CancellationToken ct = default);
    Task<bool> UserExistsAsync(string email, CancellationToken ct = default);
    Task<Guid?> FindUserIdByEmailAsync(string email, CancellationToken ct = default);
}
