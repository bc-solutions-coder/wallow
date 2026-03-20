using Wallow.Identity.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Wallow.Identity.Infrastructure.Data;

public sealed partial class IdentityDataSeeder(ILogger<IdentityDataSeeder> logger)
{
    private static readonly string[] _defaultRoles = ["admin", "manager", "user"];

    public async Task SeedAsync(
        RoleManager<WallowRole> roleManager,
        UserManager<WallowUser> userManager,
        IOpenIddictApplicationManager applicationManager,
        TimeProvider timeProvider,
        CancellationToken ct = default)
    {
        await SeedRolesAsync(roleManager);
        await SeedAdminUserAsync(userManager, timeProvider);
        await SeedDevClientAsync(applicationManager, ct);
    }

    private async Task SeedRolesAsync(RoleManager<WallowRole> roleManager)
    {
        foreach (string roleName in _defaultRoles)
        {
            if (await roleManager.RoleExistsAsync(roleName))
            {
                continue;
            }

            WallowRole role = new()
            {
                Id = Guid.NewGuid(),
                Name = roleName,
                NormalizedName = roleName.ToUpperInvariant(),
                TenantId = Guid.Empty
            };

            IdentityResult result = await roleManager.CreateAsync(role);
            if (result.Succeeded)
            {
                LogRoleSeeded(roleName);
            }
            else
            {
                LogRoleSeedFailed(roleName, string.Join(", ", result.Errors.Select(e => e.Description)));
            }
        }
    }

    private async Task SeedAdminUserAsync(
        UserManager<WallowUser> userManager,
        TimeProvider timeProvider)
    {
        const string adminEmail = "admin@wallow.dev";
        const string adminPassword = "Admin123!";

        WallowUser? existingUser = await userManager.FindByEmailAsync(adminEmail);
        if (existingUser is not null)
        {
            return;
        }

        WallowUser adminUser = WallowUser.Create(
            tenantId: Guid.Empty,
            firstName: "Admin",
            lastName: "User",
            email: adminEmail,
            timeProvider: timeProvider);

        adminUser.EmailConfirmed = true;

        IdentityResult createResult = await userManager.CreateAsync(adminUser, adminPassword);
        if (!createResult.Succeeded)
        {
            LogAdminUserSeedFailed(string.Join(", ", createResult.Errors.Select(e => e.Description)));
            return;
        }

        IdentityResult roleResult = await userManager.AddToRoleAsync(adminUser, "admin");
        if (roleResult.Succeeded)
        {
            LogAdminUserSeeded(adminEmail);
        }
        else
        {
            LogAdminRoleAssignFailed(string.Join(", ", roleResult.Errors.Select(e => e.Description)));
        }
    }

    private async Task SeedDevClientAsync(
        IOpenIddictApplicationManager applicationManager,
        CancellationToken ct)
    {
        const string clientId = "wallow-dev-client";

        object? existing = await applicationManager.FindByClientIdAsync(clientId, ct);
        if (existing is not null)
        {
            return;
        }

        OpenIddictApplicationDescriptor descriptor = new()
        {
            ClientId = clientId,
            DisplayName = "Wallow Dev Client",
            ClientType = ClientTypes.Public,
            RedirectUris =
            {
                new Uri("http://localhost:5000/callback"),
                new Uri("http://localhost:3000/callback")
            },
            PostLogoutRedirectUris =
            {
                new Uri("http://localhost:5000"),
                new Uri("http://localhost:3000")
            },
            Permissions =
            {
                Permissions.Endpoints.Authorization,
                Permissions.Endpoints.Token,
                Permissions.GrantTypes.AuthorizationCode,
                Permissions.GrantTypes.RefreshToken,
                Permissions.ResponseTypes.Code,
                Permissions.Scopes.Email,
                Permissions.Scopes.Profile,
                Permissions.Scopes.Roles,
                Permissions.Prefixes.Scope + "api"
            },
            Requirements =
            {
                Requirements.Features.ProofKeyForCodeExchange
            }
        };

        await applicationManager.CreateAsync(descriptor, ct);
        LogDevClientSeeded(clientId);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Seeded role: {RoleName}")]
    private partial void LogRoleSeeded(string roleName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to seed role {RoleName}: {Errors}")]
    private partial void LogRoleSeedFailed(string roleName, string errors);

    [LoggerMessage(Level = LogLevel.Information, Message = "Seeded admin user: {Email}")]
    private partial void LogAdminUserSeeded(string email);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to seed admin user: {Errors}")]
    private partial void LogAdminUserSeedFailed(string errors);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to assign admin role: {Errors}")]
    private partial void LogAdminRoleAssignFailed(string errors);

    [LoggerMessage(Level = LogLevel.Information, Message = "Seeded dev OAuth2 client: {ClientId}")]
    private partial void LogDevClientSeeded(string clientId);
}
