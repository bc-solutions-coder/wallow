using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Wallow.Identity.Application.Commands.BootstrapAdmin;
using Wallow.Identity.Application.Queries.IsSetupRequired;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Infrastructure.Persistence;
using Wallow.Identity.Infrastructure.Services;

namespace Wallow.SeederService;

public sealed partial class SeederWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<SeedOptions> seedOptions,
    IHostApplicationLifetime lifetime,
    ILogger<SeederWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        LogSeederStarted();

        try
        {
            await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
            IServiceProvider sp = scope.ServiceProvider;

            await SeedRolesAsync(sp);
            await SeedApiScopesAsync(sp, stoppingToken);
            await BootstrapAdminAsync(sp, stoppingToken);
            await SyncClientsAsync(sp, stoppingToken);

            LogSeederCompleted();
        }
        catch (Exception ex)
        {
            LogSeederFailed(ex);
            throw;
        }
        finally
        {
            lifetime.StopApplication();
        }
    }

    private async Task SeedRolesAsync(IServiceProvider sp)
    {
        LogStepStarted("Seed Roles");

        RoleManager<WallowRole> roleManager = sp.GetRequiredService<RoleManager<WallowRole>>();

        foreach (string roleName in seedOptions.Value.Roles)
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

        LogStepCompleted("Seed Roles");
    }

    private async Task SeedApiScopesAsync(IServiceProvider sp, CancellationToken ct)
    {
        LogStepStarted("Seed API Scopes");

        IdentityDbContext dbContext = sp.GetRequiredService<IdentityDbContext>();

        List<string> existingCodes = await dbContext.ApiScopes
            .Select(s => s.Code)
            .ToListAsync(ct);

        List<ApiScope> newScopes = seedOptions.Value.ApiScopes
            .Where(s => !existingCodes.Contains(s.Code))
            .Select(s => ApiScope.Create(s.Code, s.DisplayName, s.Category, s.Description, s.IsDefault))
            .ToList();

        if (newScopes.Count > 0)
        {
            foreach (ApiScope scope in newScopes)
            {
                dbContext.ApiScopes.Add(scope);
            }

            await dbContext.SaveChangesAsync(ct);
            LogApiScopesSeeded(newScopes.Count);
        }

        LogStepCompleted("Seed API Scopes");
    }

    private async Task BootstrapAdminAsync(IServiceProvider sp, CancellationToken ct)
    {
        LogStepStarted("Bootstrap Admin");

        ISetupStatusChecker setupStatusChecker = sp.GetRequiredService<ISetupStatusChecker>();
        bool setupRequired = await setupStatusChecker.IsSetupRequiredAsync(ct);

        if (setupRequired && seedOptions.Value.Admin is { IsConfigured: true } admin)
        {
            IBootstrapAdminService bootstrapAdminService = sp.GetRequiredService<IBootstrapAdminService>();
            await bootstrapAdminService.EnsureRoleExistsAsync("admin", ct);

            bool userExists = await bootstrapAdminService.UserExistsAsync(admin.Email, ct);
            if (!userExists)
            {
                Guid userId = await bootstrapAdminService.CreateUserAsync(
                    admin.Email,
                    admin.Password,
                    admin.FirstName,
                    admin.LastName,
                    ct);
                await bootstrapAdminService.AssignRoleAsync(userId, "admin", ct);
                LogAdminBootstrapped(admin.Email);
            }
        }

        LogStepCompleted("Bootstrap Admin");
    }

    private async Task SyncClientsAsync(IServiceProvider sp, CancellationToken ct)
    {
        LogStepStarted("Client Sync");

        PreRegisteredClientSyncService clientSync = sp.GetRequiredService<PreRegisteredClientSyncService>();
        await clientSync.SyncAsync(ct);

        LogStepCompleted("Client Sync");
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Seeder worker started")]
    private partial void LogSeederStarted();

    [LoggerMessage(Level = LogLevel.Information, Message = "Step '{StepName}' started")]
    private partial void LogStepStarted(string stepName);

    [LoggerMessage(Level = LogLevel.Information, Message = "Step '{StepName}' completed")]
    private partial void LogStepCompleted(string stepName);

    [LoggerMessage(Level = LogLevel.Information, Message = "Seeded role: {RoleName}")]
    private partial void LogRoleSeeded(string roleName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to seed role {RoleName}: {Errors}")]
    private partial void LogRoleSeedFailed(string roleName, string errors);

    [LoggerMessage(Level = LogLevel.Information, Message = "Seeded {Count} API scopes")]
    private partial void LogApiScopesSeeded(int count);

    [LoggerMessage(Level = LogLevel.Information, Message = "Bootstrapped admin user: {Email}")]
    private partial void LogAdminBootstrapped(string email);

    [LoggerMessage(Level = LogLevel.Information, Message = "Seeder worker completed successfully")]
    private partial void LogSeederCompleted();

    [LoggerMessage(Level = LogLevel.Critical, Message = "Seeder worker failed")]
    private partial void LogSeederFailed(Exception ex);
}
