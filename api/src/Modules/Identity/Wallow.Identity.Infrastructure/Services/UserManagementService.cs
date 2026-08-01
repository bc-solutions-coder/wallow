using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Wallow.Identity.Application.DTOs;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Domain.Identity;
using Wallow.Identity.Infrastructure.Persistence;
using Wallow.Shared.Contracts.Identity.Events;
using Wallow.Shared.Kernel.MultiTenancy;
using Wolverine;

namespace Wallow.Identity.Infrastructure.Services;

public sealed partial class UserManagementService(
    UserManager<WallowUser> userManager,
    RoleManager<WallowRole> roleManager,
    IdentityDbContext dbContext,
    IMembershipRepository membershipRepository,
    IMessageBus messageBus,
    ITenantContext tenantContext,
    TimeProvider timeProvider,
    ILogger<UserManagementService> logger) : IUserManagementService
{
    private const string DefaultRoleName = "user";

    public async Task<Guid> CreateUserAsync(
        string email,
        string firstName,
        string lastName,
        string? password = null,
        CancellationToken ct = default)
    {
        LogCreatingUser(email);

        WallowUser user = WallowUser.Create(
            tenantContext.TenantId.Value,
            firstName,
            lastName,
            email,
            timeProvider);

        IdentityResult result = string.IsNullOrWhiteSpace(password)
            ? await userManager.CreateAsync(user)
            : await userManager.CreateAsync(user, password);

        if (!result.Succeeded)
        {
            string errors = string.Join(", ", result.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Failed to create user: {errors}");
        }

        // An administrator creating a user creates them INTO the organization being administered:
        // the membership is what carries the default role, and without one the new account
        // resolves no roles anywhere.
        Guid organizationId = tenantContext.TenantId.Value;
        if (organizationId != Guid.Empty)
        {
            membershipRepository.Add(Membership.Enroll(
                user.Id,
                OrganizationId.Create(organizationId),
                await ResolveRoleIdAsync(DefaultRoleName, ct),
                timeProvider));

            await membershipRepository.SaveChangesAsync(ct);
        }

        await messageBus.PublishAsync(new UserRegisteredEvent
        {
            UserId = user.Id,
            TenantId = tenantContext.TenantId.Value,
            Email = email,
            FirstName = firstName,
            LastName = lastName
        });

        LogUserCreated(email, user.Id);

        return user.Id;
    }

    public async Task<UserDto?> GetUserByIdAsync(Guid userId, CancellationToken ct = default)
    {
        WallowUser? user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return null;
        }

        Dictionary<Guid, List<string>> rolesByUserId = await GetRolesByUserIdsAsync([user.Id], ct);
        rolesByUserId.TryGetValue(user.Id, out List<string>? roles);

        return new UserDto(
            user.Id,
            user.Email ?? string.Empty,
            user.FirstName,
            user.LastName,
            user.IsActive,
            roles?.AsReadOnly() ?? new List<string>().AsReadOnly());
    }

    public async Task<UserDto?> GetUserByEmailAsync(string email, CancellationToken ct = default)
    {
        WallowUser? user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            return null;
        }

        Dictionary<Guid, List<string>> rolesByUserId = await GetRolesByUserIdsAsync([user.Id], ct);
        rolesByUserId.TryGetValue(user.Id, out List<string>? roles);

        return new UserDto(
            user.Id,
            user.Email ?? string.Empty,
            user.FirstName,
            user.LastName,
            user.IsActive,
            roles?.AsReadOnly() ?? new List<string>().AsReadOnly());
    }

    public async Task<IReadOnlyList<UserDto>> GetUsersAsync(
        string? search = null,
        int first = 0,
        int max = 20,
        CancellationToken ct = default)
    {
        IQueryable<WallowUser> query = userManager.Users;

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(u =>
                (u.Email != null && u.Email.Contains(search, StringComparison.OrdinalIgnoreCase)) ||
                u.FirstName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                u.LastName.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        List<WallowUser> users = await query
            .OrderBy(u => u.Email)
            .Skip(first)
            .Take(max)
            .ToListAsync(ct);

        Dictionary<Guid, List<string>> rolesByUserId = await GetRolesByUserIdsAsync(
            users.Select(u => u.Id).ToList(), ct);

        List<UserDto> result = new(users.Count);
        foreach (WallowUser user in users)
        {
            rolesByUserId.TryGetValue(user.Id, out List<string>? roles);
            result.Add(new UserDto(
                user.Id,
                user.Email ?? string.Empty,
                user.FirstName,
                user.LastName,
                user.IsActive,
                roles?.AsReadOnly() ?? new List<string>().AsReadOnly()));
        }

        return result;
    }

    private async Task<Dictionary<Guid, List<string>>> GetRolesByUserIdsAsync(
        List<Guid> userIds,
        CancellationToken ct)
    {
        List<UserRoleMapping> userRoleMappings = await dbContext.UserRoles
            .Where(ur => userIds.Contains(ur.UserId))
            .Join(
                dbContext.Roles,
                ur => ur.RoleId,
                r => r.Id,
                (ur, r) => new UserRoleMapping(ur.UserId, r.Name!))
            .ToListAsync(ct);

        Dictionary<Guid, List<string>> rolesByUserId = userRoleMappings
            .GroupBy(x => x.UserId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(x => x.RoleName).ToList());

        return rolesByUserId;
    }

    private sealed record UserRoleMapping(Guid UserId, string RoleName);

    public async Task DeactivateUserAsync(Guid userId, CancellationToken ct = default)
    {
        LogDeactivatingUser(userId);

        WallowUser? user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            throw new InvalidOperationException($"User '{userId}' not found");
        }

        await userManager.SetLockoutEnabledAsync(user, true);
        await userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);

        LogUserDeactivated(userId);
    }

    public async Task ActivateUserAsync(Guid userId, CancellationToken ct = default)
    {
        LogActivatingUser(userId);

        WallowUser? user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            throw new InvalidOperationException($"User '{userId}' not found");
        }

        await userManager.SetLockoutEnabledAsync(user, false);
        await userManager.SetLockoutEndDateAsync(user, null);

        LogUserActivated(userId);
    }

    public async Task AssignRoleAsync(Guid userId, Guid organizationId, string roleName, CancellationToken ct = default)
    {
        LogAssigningRole(roleName, userId);

        bool roleExists = await roleManager.RoleExistsAsync(roleName);
        if (!roleExists)
        {
            throw new InvalidOperationException($"Role '{roleName}' not found");
        }

        WallowUser? user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            throw new InvalidOperationException($"User '{userId}' not found");
        }

        Membership membership = await RequiredMembershipAsync(userId, organizationId, ct);
        IReadOnlyList<string> currentRoles = await RoleNamesAsync(membership, ct);
        string oldRole = currentRoles.FirstOrDefault(r => r != roleName) ?? "none";

        membership.AssignRole(await ResolveRoleIdAsync(roleName, ct), userId, timeProvider);
        await membershipRepository.SaveChangesAsync(ct);

        await messageBus.PublishAsync(new UserRoleChangedEvent
        {
            UserId = userId,
            TenantId = organizationId,
            Email = user.Email ?? string.Empty,
            OldRole = oldRole,
            NewRole = roleName
        });

        LogRoleAssigned(roleName, userId);
    }

    public async Task RemoveRoleAsync(Guid userId, Guid organizationId, string roleName, CancellationToken ct = default)
    {
        LogRemovingRole(roleName, userId);

        WallowUser? user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            throw new InvalidOperationException($"User '{userId}' not found");
        }

        Membership membership = await RequiredMembershipAsync(userId, organizationId, ct);

        membership.RemoveRole(await ResolveRoleIdAsync(roleName, ct), userId, timeProvider);
        await membershipRepository.SaveChangesAsync(ct);

        IReadOnlyList<string> currentRoles = await RoleNamesAsync(membership, ct);
        string newRole = currentRoles.Count > 0 ? currentRoles[0] : "none";

        await messageBus.PublishAsync(new UserRoleChangedEvent
        {
            UserId = userId,
            TenantId = organizationId,
            Email = user.Email ?? string.Empty,
            OldRole = roleName,
            NewRole = newRole
        });

        LogRoleRemoved(roleName, userId);
    }

    public async Task<IReadOnlyList<string>> GetUserRolesAsync(Guid userId, Guid organizationId, CancellationToken ct = default)
    {
        Membership? membership = await membershipRepository.GetAsync(userId, organizationId, ct);

        return membership is null ? [] : await RoleNamesAsync(membership, ct);
    }

    public async Task DeleteUserAsync(Guid userId, CancellationToken ct = default)
    {
        LogDeletingUser(userId);

        WallowUser? user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            throw new InvalidOperationException($"User '{userId}' not found");
        }

        IdentityResult result = await userManager.DeleteAsync(user);
        if (!result.Succeeded)
        {
            string errors = string.Join(", ", result.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Failed to delete user: {errors}");
        }

        LogUserDeleted(userId);
    }

    /// <summary>
    /// The membership the role write lands on. Absent it there is nowhere to record the grant,
    /// and inventing one here would let a role assignment double as an enrollment, bypassing the
    /// organization's own enrollment policy.
    /// </summary>
    private async Task<Membership> RequiredMembershipAsync(Guid userId, Guid organizationId, CancellationToken ct)
    {
        Membership? membership = await membershipRepository.GetAsync(userId, organizationId, ct);

        return membership
            ?? throw new InvalidOperationException(
                $"User '{userId}' is not a member of organization '{organizationId}'");
    }

    private async Task<IReadOnlyList<string>> RoleNamesAsync(Membership membership, CancellationToken ct)
    {
        List<Guid> roleIds = [.. membership.RoleIds];
        if (roleIds.Count == 0)
        {
            return [];
        }

        // The role catalog is global: roles are seeded with an empty tenant id and addressed by
        // id, so no tenant scoping applies to the lookup.
        return await dbContext.Roles
            .IgnoreQueryFilters()
            .Where(r => roleIds.Contains(r.Id) && r.Name != null)
            .Select(r => r.Name!)
            .ToListAsync(ct);
    }

    private async Task<Guid> ResolveRoleIdAsync(string roleName, CancellationToken ct)
    {
        // Identity's default normalizer upper-cases invariantly, so this matches what
        // RoleManager wrote without paying for a case-insensitive collation scan.
        string normalizedName = roleName.ToUpperInvariant();

        WallowRole? role = await dbContext.Roles
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.NormalizedName == normalizedName, ct);

        return role?.Id ?? throw new InvalidOperationException($"Role '{roleName}' not found");
    }
}

public sealed partial class UserManagementService
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Creating user {Email}")]
    private partial void LogCreatingUser(string email);

    [LoggerMessage(Level = LogLevel.Information, Message = "User {Email} created with ID {UserId}")]
    private partial void LogUserCreated(string email, Guid userId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Deactivating user {UserId}")]
    private partial void LogDeactivatingUser(Guid userId);

    [LoggerMessage(Level = LogLevel.Information, Message = "User {UserId} deactivated")]
    private partial void LogUserDeactivated(Guid userId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Activating user {UserId}")]
    private partial void LogActivatingUser(Guid userId);

    [LoggerMessage(Level = LogLevel.Information, Message = "User {UserId} activated")]
    private partial void LogUserActivated(Guid userId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Assigning role {RoleName} to user {UserId}")]
    private partial void LogAssigningRole(string roleName, Guid userId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Role {RoleName} assigned to user {UserId}")]
    private partial void LogRoleAssigned(string roleName, Guid userId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Removing role {RoleName} from user {UserId}")]
    private partial void LogRemovingRole(string roleName, Guid userId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Role {RoleName} removed from user {UserId}")]
    private partial void LogRoleRemoved(string roleName, Guid userId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Deleting user {UserId}")]
    private partial void LogDeletingUser(Guid userId);

    [LoggerMessage(Level = LogLevel.Information, Message = "User {UserId} deleted")]
    private partial void LogUserDeleted(Guid userId);
}
