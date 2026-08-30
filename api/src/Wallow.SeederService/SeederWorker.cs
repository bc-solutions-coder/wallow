using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Wallow.Identity.Application.Commands.BootstrapAdmin;
using Wallow.Identity.Application.Queries.IsSetupRequired;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Infrastructure.Persistence;
using Wallow.Identity.Infrastructure.Services;
using Wallow.ServiceDefaults;
using Wallow.Shared.Kernel.Results;

namespace Wallow.SeederService;

public sealed partial class SeederWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<SeedOptions> seedOptions,
    IHostApplicationLifetime lifetime,
    WorkerRunOutcome outcome,
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
            await SyncOpenIddictScopesAsync(sp, stoppingToken);
            await BootstrapAdminAsync(sp, stoppingToken);
            await SyncOrganizationsAsync(sp, stoppingToken);
            await SyncClientsAsync(sp, stoppingToken);

            LogSeederCompleted();
        }
        catch (Exception ex)
        {
            // The host swallows this after logging it: RunAsync() still completes successfully and
            // the process would exit 0. Program.cs reads this flag to exit non-zero instead.
            outcome.MarkFailed();
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
            .Select(s => ApiScope.Create(s.Code, s.DisplayName, s.Category, s.Description, s.IsDefault, s.PlatformOnly))
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

    /// <summary>
    /// After the catalog, because it mirrors it: a scope seeded this run must reach the consent
    /// screen this run, not the next one.
    /// </summary>
    private async Task SyncOpenIddictScopesAsync(IServiceProvider sp, CancellationToken ct)
    {
        LogStepStarted("Sync OpenIddict Scopes");

        OpenIddictScopeSyncService scopeSync = sp.GetRequiredService<OpenIddictScopeSyncService>();
        await scopeSync.SyncAsync(ct);

        LogStepCompleted("Sync OpenIddict Scopes");
    }

    internal async Task BootstrapAdminAsync(IServiceProvider sp, CancellationToken ct)
    {
        LogStepStarted("Bootstrap Admin");

        if (seedOptions.Value.Admin is not { IsConfigured: true } admin)
        {
            LogSeedAdminNotConfigured();
            LogStepCompleted("Bootstrap Admin");
            return;
        }

        // Consulting the gate reverses Wallow-wd6n, and is only correct because bootstrap now
        // goes through BootstrapAdminCommand: the command mints the organization and the owner
        // membership itself, so a closed gate means a fully-provisioned administrator exists
        // and a re-seed must not fight the setup page's outcome. Under the old user-only
        // bootstrap, the gate closing said nothing about the seed admin, so consulting it
        // silently suppressed the configured account.
        ISetupStatusChecker setupStatusChecker = sp.GetRequiredService<ISetupStatusChecker>();
        bool setupRequired = await setupStatusChecker.IsSetupRequiredAsync(ct);
        if (!setupRequired)
        {
            LogSetupAlreadyComplete(admin.Email);
            LogStepCompleted("Bootstrap Admin");
            return;
        }

        IBootstrapAdminService bootstrapAdminService = sp.GetRequiredService<IBootstrapAdminService>();

        // The gate can be open while the account exists (a half-bootstrapped user with no admin
        // membership). Creating on top of it would fail; leave it for a human to resolve.
        bool userExists = await bootstrapAdminService.UserExistsAsync(admin.Email, ct);
        if (userExists)
        {
            LogSeedAdminAlreadyExists(admin.Email);
            LogStepCompleted("Bootstrap Admin");
            return;
        }

        // The same command the setup page's POST /v1/identity/setup/admin invokes: user +
        // organization + owner membership carrying the admin role. The seeder must not own a
        // second, weaker definition of "bootstrap an admin".
        BootstrapAdminHandler bootstrapAdminHandler = sp.GetRequiredService<BootstrapAdminHandler>();
        BootstrapAdminCommand command = new(
            admin.Email,
            admin.Password,
            admin.FirstName,
            admin.LastName,
            admin.OrganizationName);

        Result result = await bootstrapAdminHandler.Handle(command, ct);
        if (result.IsFailure)
        {
            throw new InvalidOperationException($"Admin bootstrap failed: {result.Error.Message}");
        }

        // Seeder-only post-step: no runtime endpoint grants the global-administrator claim.
        if (admin.IsGlobalAdmin)
        {
            Guid userId = await bootstrapAdminService.FindUserIdByEmailAsync(admin.Email, ct)
                ?? throw new InvalidOperationException(
                    $"Admin bootstrap reported success but no user exists for '{admin.Email}'.");
            await bootstrapAdminService.GrantGlobalAdminAsync(userId, ct);
            LogGlobalAdminGranted(admin.Email);
        }

        LogAdminBootstrapped(admin.Email);

        LogStepCompleted("Bootstrap Admin");
    }

    /// <summary>
    /// Before the clients, because a client binds to an organization by name: the organization it
    /// finds must already admit people on the configured terms, not on the InviteOnly default a
    /// client-created one would carry until the next run.
    /// </summary>
    private async Task SyncOrganizationsAsync(IServiceProvider sp, CancellationToken ct)
    {
        LogStepStarted("Sync Organizations");

        OrganizationSeedSyncService organizationSync = sp.GetRequiredService<OrganizationSeedSyncService>();
        await organizationSync.SyncAsync(ct);

        LogStepCompleted("Sync Organizations");
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

    [LoggerMessage(Level = LogLevel.Warning, Message = "No seed admin is configured (email, password and organizationName required); skipping admin bootstrap")]
    private partial void LogSeedAdminNotConfigured();

    [LoggerMessage(Level = LogLevel.Information, Message = "Setup is already complete; leaving the existing administration alone instead of bootstrapping seed admin {Email}")]
    private partial void LogSetupAlreadyComplete(string email);

    [LoggerMessage(Level = LogLevel.Information, Message = "Seed admin user {Email} already exists; leaving it untouched")]
    private partial void LogSeedAdminAlreadyExists(string email);

    [LoggerMessage(Level = LogLevel.Information, Message = "Bootstrapped admin user: {Email}")]
    private partial void LogAdminBootstrapped(string email);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Granted the global administrator claim to seeded admin user: {Email}")]
    private partial void LogGlobalAdminGranted(string email);

    [LoggerMessage(Level = LogLevel.Information, Message = "Seeder worker completed successfully")]
    private partial void LogSeederCompleted();

    [LoggerMessage(Level = LogLevel.Critical, Message = "Seeder worker failed")]
    private partial void LogSeederFailed(Exception ex);
}
