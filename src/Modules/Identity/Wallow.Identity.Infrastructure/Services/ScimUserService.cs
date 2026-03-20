using System.Globalization;
using Wallow.Identity.Application.DTOs;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Domain.Enums;
using Wallow.Identity.Infrastructure.Scim;
using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.MultiTenancy;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Wallow.Identity.Infrastructure.Services;

public sealed partial class ScimUserService(
    UserManager<WallowUser> userManager,
    RoleManager<WallowRole> roleManager,
    IOrganizationService organizationService,
    IScimConfigurationRepository scimRepository,
    IScimSyncLogRepository syncLogRepository,
    ITenantContext tenantContext,
    ILogger<ScimUserService> logger,
    TimeProvider timeProvider)
{
    public async Task<ScimUser> CreateUserAsync(ScimUserRequest request, CancellationToken ct = default)
    {
        TenantId tenantId = tenantContext.TenantId;
        string externalId = request.ExternalId ?? Guid.NewGuid().ToString();

        LogCreatingScimUser(request.UserName, tenantId.Value);

        try
        {
            string email = request.Emails?.FirstOrDefault(e => e.Primary)?.Value ?? request.UserName;
            string firstName = request.Name?.GivenName ?? string.Empty;
            string lastName = request.Name?.FamilyName ?? string.Empty;

            WallowUser user = WallowUser.Create(
                tenantId.Value,
                string.IsNullOrWhiteSpace(firstName) ? "SCIM" : firstName,
                string.IsNullOrWhiteSpace(lastName) ? "User" : lastName,
                email,
                timeProvider);

            user.UserName = request.UserName;

            IdentityResult result = await userManager.CreateAsync(user);
            if (!result.Succeeded)
            {
                string errors = string.Join(", ", result.Errors.Select(e => e.Description));
                if (result.Errors.Any(e => e.Code == "DuplicateUserName" || e.Code == "DuplicateEmail"))
                {
                    throw new InvalidOperationException($"User '{request.UserName}' already exists");
                }

                throw new InvalidOperationException($"Failed to create user: {errors}");
            }

            string userId = user.Id.ToString();

            await AddUserToOrganizationAsync(user.Id, ct);
            await AssignDefaultRoleAsync(user.Id, ct);
            await LogSyncAsync(ScimOperation.Create, ScimResourceType.User, externalId, userId, true, ct: ct);

            LogScimUserCreated(request.UserName, userId);

            return await GetUserAsync(userId, ct) ?? throw new InvalidOperationException("Failed to retrieve created user");
        }
        catch (Exception ex)
        {
            LogCreateScimUserFailed(ex, request.UserName);
            await LogSyncAsync(ScimOperation.Create, ScimResourceType.User, externalId, null, false, ex.Message, ct: ct);
            throw;
        }
    }

    public async Task<ScimUser> UpdateUserAsync(string id, ScimUserRequest request, CancellationToken ct = default)
    {
        string externalId = request.ExternalId ?? id;

        LogUpdatingScimUser(id);

        try
        {
            WallowUser? user = await userManager.FindByIdAsync(id);
            if (user is null)
            {
                throw new InvalidOperationException($"User '{id}' not found");
            }

            string email = request.Emails?.FirstOrDefault(e => e.Primary)?.Value ?? request.UserName;

            user.UserName = request.UserName;
            user.Email = email;

            IdentityResult result = await userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                string errors = string.Join(", ", result.Errors.Select(e => e.Description));
                throw new InvalidOperationException($"Failed to update user: {errors}");
            }

            if (!request.Active)
            {
                await userManager.SetLockoutEnabledAsync(user, true);
                await userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);
            }
            else
            {
                await userManager.SetLockoutEnabledAsync(user, false);
                await userManager.SetLockoutEndDateAsync(user, null);
            }

            await LogSyncAsync(ScimOperation.Update, ScimResourceType.User, externalId, id, true, ct: ct);

            LogScimUserUpdated(id);

            return await GetUserAsync(id, ct) ?? throw new InvalidOperationException("Failed to retrieve updated user");
        }
        catch (Exception ex)
        {
            LogUpdateScimUserFailed(ex, id);
            await LogSyncAsync(ScimOperation.Update, ScimResourceType.User, externalId, id, false, ex.Message, ct: ct);
            throw;
        }
    }

    public async Task<ScimUser> PatchUserAsync(string id, ScimPatchRequest request, CancellationToken ct = default)
    {
        LogPatchingScimUser(id, request.Operations.Count);

        try
        {
            WallowUser? user = await userManager.FindByIdAsync(id);
            if (user is null)
            {
                throw new InvalidOperationException($"User {id} not found");
            }

            bool activeChanged = false;
            bool? newActiveState = null;

            foreach (ScimPatchOperation op in request.Operations)
            {
                ApplyPatchOperation(user, op, ref activeChanged, ref newActiveState);
            }

            IdentityResult result = await userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                string errors = string.Join(", ", result.Errors.Select(e => e.Description));
                throw new InvalidOperationException($"Failed to patch user: {errors}");
            }

            if (activeChanged && newActiveState.HasValue)
            {
                if (!newActiveState.Value)
                {
                    await userManager.SetLockoutEnabledAsync(user, true);
                    await userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);
                }
                else
                {
                    await userManager.SetLockoutEnabledAsync(user, false);
                    await userManager.SetLockoutEndDateAsync(user, null);
                }
            }

            await LogSyncAsync(ScimOperation.Patch, ScimResourceType.User, id, id, true, ct: ct);

            LogScimUserPatched(id);

            return await GetUserAsync(id, ct) ?? throw new InvalidOperationException("Failed to retrieve patched user");
        }
        catch (Exception ex)
        {
            LogPatchScimUserFailed(ex, id);
            await LogSyncAsync(ScimOperation.Patch, ScimResourceType.User, id, id, false, ex.Message, ct: ct);
            throw;
        }
    }

    public async Task DeleteUserAsync(string id, CancellationToken ct = default)
    {
        LogDeletingScimUser(id);

        try
        {
            WallowUser? user = await userManager.FindByIdAsync(id);
            if (user is null)
            {
                throw new InvalidOperationException($"User '{id}' not found");
            }

            ScimConfiguration? config = await scimRepository.GetAsync(ct);

            if (config?.DeprovisionOnDelete == true)
            {
                IdentityResult result = await userManager.DeleteAsync(user);
                if (!result.Succeeded)
                {
                    string errors = string.Join(", ", result.Errors.Select(e => e.Description));
                    throw new InvalidOperationException($"Failed to delete user: {errors}");
                }

                LogScimUserDeleted(id);
            }
            else
            {
                await userManager.SetLockoutEnabledAsync(user, true);
                await userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);
                LogScimUserDisabled(id);
            }

            await LogSyncAsync(ScimOperation.Delete, ScimResourceType.User, id, id, true, ct: ct);
        }
        catch (Exception ex)
        {
            LogDeleteScimUserFailed(ex, id);
            await LogSyncAsync(ScimOperation.Delete, ScimResourceType.User, id, id, false, ex.Message, ct: ct);
            throw;
        }
    }

    public async Task<ScimUser?> GetUserAsync(string id, CancellationToken _ = default)
    {
        try
        {
            WallowUser? user = await userManager.FindByIdAsync(id);
            if (user is null)
            {
                return null;
            }

            return MapToScimUser(user);
        }
        catch (Exception ex)
        {
            LogGetScimUserFailed(ex, id);
            return null;
        }
    }

    public async Task<ScimListResponse<ScimUser>> ListUsersAsync(ScimListRequest request, CancellationToken ct = default)
    {
        int skip = Math.Max(0, request.StartIndex - 1);
        int take = Math.Min(request.Count, ScimConstants.MaxPageSize);

        IQueryable<WallowUser> query = userManager.Users;

        ScimAttributeMapper mapper = new();
        ScimFilterParams filterParams = mapper.Translate(request.Filter);

        if (filterParams.UserName != null)
        {
            string username = filterParams.UserName;
            query = query.Where(u => u.UserName != null && EF.Functions.ILike(u.UserName, username));
        }

        if (filterParams.Email != null)
        {
            string email = filterParams.Email;
            query = query.Where(u => u.Email != null && EF.Functions.ILike(u.Email, email));
        }

        if (filterParams.FirstName != null)
        {
            string pattern = $"%{filterParams.FirstName}%";
            query = query.Where(u => EF.Functions.ILike(u.FirstName, pattern));
        }

        if (filterParams.LastName != null)
        {
            string pattern = $"%{filterParams.LastName}%";
            query = query.Where(u => EF.Functions.ILike(u.LastName, pattern));
        }

        if (filterParams.Search != null)
        {
            string pattern = $"%{filterParams.Search}%";
            query = query.Where(u =>
                (u.Email != null && EF.Functions.ILike(u.Email, pattern)) ||
                EF.Functions.ILike(u.FirstName, pattern) ||
                EF.Functions.ILike(u.LastName, pattern) ||
                (u.UserName != null && EF.Functions.ILike(u.UserName, pattern)));
        }

        int totalCount = await query.CountAsync(ct);

        List<WallowUser> users = await query
            .OrderBy(u => u.Email)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);

        List<ScimUser> scimUsers = users.Select(MapToScimUser).ToList();

        if (filterParams.InMemoryFilter != null)
        {
            scimUsers = scimUsers.Where(filterParams.InMemoryFilter).ToList();
        }

        return new ScimListResponse<ScimUser>
        {
            TotalResults = filterParams.InMemoryFilter != null ? scimUsers.Count : totalCount,
            StartIndex = request.StartIndex,
            ItemsPerPage = scimUsers.Count,
            Resources = scimUsers
        };
    }

    private async Task AddUserToOrganizationAsync(Guid userId, CancellationToken ct)
    {
        TenantId tenantId = tenantContext.TenantId;

        try
        {
            await organizationService.AddMemberAsync(tenantId.Value, userId, ct);
        }
        catch (Exception ex)
        {
            LogAddUserToOrgException(ex, userId.ToString());
        }
    }

    private async Task AssignDefaultRoleAsync(Guid userId, CancellationToken ct)
    {
        ScimConfiguration? config = await scimRepository.GetAsync(ct);
        if (string.IsNullOrWhiteSpace(config?.DefaultRole))
        {
            return;
        }

        try
        {
            bool roleExists = await roleManager.RoleExistsAsync(config.DefaultRole);
            if (!roleExists)
            {
                LogDefaultRoleNotFound(config.DefaultRole);
                return;
            }

            WallowUser? user = await userManager.FindByIdAsync(userId.ToString());
            if (user is null)
            {
                return;
            }

            IdentityResult result = await userManager.AddToRoleAsync(user, config.DefaultRole);
            if (!result.Succeeded)
            {
                string errors = string.Join(", ", result.Errors.Select(e => e.Description));
                LogAssignDefaultRoleFailed(null!, config.DefaultRole, userId.ToString());
            }
        }
        catch (Exception ex)
        {
            LogAssignDefaultRoleFailed(ex, config.DefaultRole, userId.ToString());
        }
    }

    private async Task LogSyncAsync(
        ScimOperation operation,
        ScimResourceType resourceType,
        string externalId,
        string? internalId,
        bool success,
        string? errorMessage = null,
        string? requestBody = null,
        CancellationToken ct = default)
    {
        try
        {
            ScimSyncLog log = ScimSyncLog.Create(
                tenantContext.TenantId,
                operation,
                resourceType,
                externalId,
                internalId,
                success,
                timeProvider,
                errorMessage,
                requestBody);

            syncLogRepository.Add(log);
            await syncLogRepository.SaveChangesAsync(ct);

            ScimConfiguration? config = await scimRepository.GetAsync(ct);
            if (config != null)
            {
                config.RecordSync(Guid.Empty, timeProvider);
                await scimRepository.SaveChangesAsync(ct);
            }
        }
        catch (Exception ex)
        {
            LogSyncLogFailed(ex);
        }
    }

    private static void ApplyPatchOperation(WallowUser user, ScimPatchOperation op, ref bool activeChanged, ref bool? newActiveState)
    {
        string? path = op.Path?.ToLowerInvariant();

        switch (op.Op.ToLowerInvariant())
        {
            case "replace":
            case "add":
                switch (path)
                {
                    case "active":
                        bool active = op.Value is bool b ? b : bool.Parse(Convert.ToString(op.Value, CultureInfo.InvariantCulture) ?? "true");
                        activeChanged = true;
                        newActiveState = active;
                        break;
                    case "username":
                    case "userName":
                        user.UserName = Convert.ToString(op.Value, CultureInfo.InvariantCulture);
                        break;
                    case "emails":
                    case "emails[type eq \"work\"].value":
                    case "emails[primary eq true].value":
                        user.Email = Convert.ToString(op.Value, CultureInfo.InvariantCulture);
                        break;
                }
                break;
            case "remove":
                break;
        }
    }

    private static ScimUser MapToScimUser(WallowUser user)
    {
        string id = user.Id.ToString();

        return new ScimUser
        {
            Id = id,
            ExternalId = id,
            UserName = user.UserName ?? user.Email ?? string.Empty,
            Name = new ScimName
            {
                GivenName = user.FirstName,
                FamilyName = user.LastName,
                Formatted = $"{user.FirstName} {user.LastName}".Trim()
            },
            DisplayName = $"{user.FirstName} {user.LastName}".Trim(),
            Emails = string.IsNullOrWhiteSpace(user.Email) ? null :
            [
                new ScimEmail
                {
                    Value = user.Email,
                    Type = "work",
                    Primary = true
                }
            ],
            Active = user.IsActive && !user.LockoutEnabled,
            Meta = new ScimMeta
            {
                ResourceType = "User",
                Location = $"/scim/v2/Users/{id}"
            }
        };
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Creating SCIM user {UserName} for tenant {TenantId}")]
    private partial void LogCreatingScimUser(string userName, Guid tenantId);

    [LoggerMessage(Level = LogLevel.Information, Message = "SCIM user {UserName} created with ID {UserId}")]
    private partial void LogScimUserCreated(string userName, string userId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to create SCIM user {UserName}")]
    private partial void LogCreateScimUserFailed(Exception ex, string userName);

    [LoggerMessage(Level = LogLevel.Information, Message = "Updating SCIM user {UserId}")]
    private partial void LogUpdatingScimUser(string userId);

    [LoggerMessage(Level = LogLevel.Information, Message = "SCIM user {UserId} updated")]
    private partial void LogScimUserUpdated(string userId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to update SCIM user {UserId}")]
    private partial void LogUpdateScimUserFailed(Exception ex, string userId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Patching SCIM user {UserId} with {OpCount} operations")]
    private partial void LogPatchingScimUser(string userId, int opCount);

    [LoggerMessage(Level = LogLevel.Information, Message = "SCIM user {UserId} patched")]
    private partial void LogScimUserPatched(string userId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to patch SCIM user {UserId}")]
    private partial void LogPatchScimUserFailed(Exception ex, string userId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Deleting SCIM user {UserId}")]
    private partial void LogDeletingScimUser(string userId);

    [LoggerMessage(Level = LogLevel.Information, Message = "SCIM user {UserId} deleted")]
    private partial void LogScimUserDeleted(string userId);

    [LoggerMessage(Level = LogLevel.Information, Message = "SCIM user {UserId} disabled")]
    private partial void LogScimUserDisabled(string userId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to delete SCIM user {UserId}")]
    private partial void LogDeleteScimUserFailed(Exception ex, string userId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to get SCIM user {UserId}")]
    private partial void LogGetScimUserFailed(Exception ex, string userId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to add user {UserId} to organization")]
    private partial void LogAddUserToOrgException(Exception ex, string userId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Role {RoleName} not found")]
    private partial void LogDefaultRoleNotFound(string roleName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to assign default role {RoleName} to user {UserId}")]
    private partial void LogAssignDefaultRoleFailed(Exception ex, string roleName, string userId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to log SCIM sync operation")]
    private partial void LogSyncLogFailed(Exception ex);
}
