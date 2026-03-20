using System.Text.Json;
using Wallow.Identity.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OpenIddict.Abstractions;
using OpenIddict.EntityFrameworkCore.Models;
using Wallow.Identity.Infrastructure.Persistence;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Wallow.Identity.Infrastructure.Data;

public sealed partial class IdentityDataSeeder(ILogger<IdentityDataSeeder> logger)
{
    private static readonly string[] _defaultRoles = ["admin", "manager", "user"];

    public async Task SeedAsync(
        RoleManager<WallowRole> roleManager,
        UserManager<WallowUser> userManager,
        IOpenIddictApplicationManager applicationManager,
        IdentityDbContext dbContext,
        TimeProvider timeProvider,
        CancellationToken ct = default)
    {
        await SeedRolesAsync(roleManager);
        await SeedAdminUserAsync(userManager, timeProvider);
        await SeedDevClientAsync(applicationManager, dbContext, ct);
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
        IdentityDbContext dbContext,
        CancellationToken ct)
    {
        const string clientId = "wallow-dev-client";

        OpenIddictApplicationDescriptor descriptor = new()
        {
            ClientId = clientId,
            DisplayName = "Wallow Dev Client",
            ClientType = ClientTypes.Public,
            RedirectUris =
            {
                new Uri("http://localhost:5000/callback"),
                new Uri("http://localhost:3000/callback"),
                new Uri("http://localhost:3000/auth/callback")
            },
            PostLogoutRedirectUris =
            {
                new Uri("http://localhost:5000/"),
                new Uri("http://localhost:3000/")
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
                Permissions.Prefixes.Scope + "offline_access",
                Permissions.Prefixes.Scope + "inquiries.read",
                Permissions.Prefixes.Scope + "inquiries.write",
                Permissions.Prefixes.Scope + "billing.read",
                Permissions.Prefixes.Scope + "billing.manage",
                Permissions.Prefixes.Scope + "invoices.read",
                Permissions.Prefixes.Scope + "invoices.write",
                Permissions.Prefixes.Scope + "payments.read",
                Permissions.Prefixes.Scope + "payments.write",
                Permissions.Prefixes.Scope + "subscriptions.read",
                Permissions.Prefixes.Scope + "subscriptions.write",
                Permissions.Prefixes.Scope + "users.read",
                Permissions.Prefixes.Scope + "users.write",
                Permissions.Prefixes.Scope + "users.manage",
                Permissions.Prefixes.Scope + "roles.read",
                Permissions.Prefixes.Scope + "roles.write",
                Permissions.Prefixes.Scope + "roles.manage",
                Permissions.Prefixes.Scope + "organizations.read",
                Permissions.Prefixes.Scope + "organizations.write",
                Permissions.Prefixes.Scope + "organizations.manage",
                Permissions.Prefixes.Scope + "apikeys.read",
                Permissions.Prefixes.Scope + "apikeys.write",
                Permissions.Prefixes.Scope + "apikeys.manage",
                Permissions.Prefixes.Scope + "sso.read",
                Permissions.Prefixes.Scope + "sso.manage",
                Permissions.Prefixes.Scope + "scim.manage",
                Permissions.Prefixes.Scope + "storage.read",
                Permissions.Prefixes.Scope + "storage.write",
                Permissions.Prefixes.Scope + "messaging.access",
                Permissions.Prefixes.Scope + "announcements.read",
                Permissions.Prefixes.Scope + "announcements.manage",
                Permissions.Prefixes.Scope + "changelog.manage",
                Permissions.Prefixes.Scope + "notifications.read",
                Permissions.Prefixes.Scope + "notifications.write",
                Permissions.Prefixes.Scope + "configuration.read",
                Permissions.Prefixes.Scope + "configuration.manage",
                Permissions.Prefixes.Scope + "serviceaccounts.read",
                Permissions.Prefixes.Scope + "serviceaccounts.write",
                Permissions.Prefixes.Scope + "serviceaccounts.manage",
                Permissions.Prefixes.Scope + "webhooks.manage"
            },
            Requirements =
            {
                Requirements.Features.ProofKeyForCodeExchange
            }
        };

        object? existing = await applicationManager.FindByClientIdAsync(clientId, ct);
        if (existing is not null)
        {
            await applicationManager.DeleteAsync(existing, ct);
        }

        await applicationManager.CreateAsync(descriptor, ct);

        // OpenIddict's EF Core store normalizes URIs via the Uri class, which strips
        // trailing slashes from root paths (e.g. "http://localhost:3000/" -> "http://localhost:3000").
        // However, OpenIddict validates post_logout_redirect_uri using exact string comparison.
        // Clients commonly send the trailing-slash form, so we patch the stored JSON directly
        // to include both variants.
        OpenIddictEntityFrameworkCoreApplication<Guid>? app = await dbContext.Set<OpenIddictEntityFrameworkCoreApplication<Guid>>()
            .FirstOrDefaultAsync(a => a.ClientId == clientId, ct);

        if (app is not null)
        {
            string[] postLogoutUris = ["http://localhost:5000", "http://localhost:5000/",
                "http://localhost:3000", "http://localhost:3000/"];
            app.PostLogoutRedirectUris = JsonSerializer.Serialize(postLogoutUris);
            await dbContext.SaveChangesAsync(ct);
        }

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
