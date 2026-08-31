using Microsoft.EntityFrameworkCore;
using Wallow.MigrationService;
using Wallow.ServiceDefaults;
using Wallow.Shared.Infrastructure.Core.Auditing;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

string connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");

// IdentityDbContext requires IDataProtectionProvider
builder.Services.AddDataProtection();

// Host-owned contexts. Auth auditing belongs to no module, so it is registered explicitly here
// rather than coming from the registry.
builder.Services.AddDbContext<AuthAuditDbContext>(options =>
    options.UseNpgsql(connectionString, npgsql =>
        npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "auth_audit")));

// Module-owned contexts, each against the schema its own module declares.
ModuleMigrations.AddModuleDbContexts(builder.Services, connectionString);

// Migration runners. Core runs first and sequentially; feature modules run in parallel afterwards.
builder.Services.AddSingleton<CoreMigrationRunners>(sp =>
{
    IServiceScopeFactory scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();
    return new CoreMigrationRunners(
    [
        .. ModuleMigrations.CreateRunners(isCore: true, scopeFactory),
        new DbContextMigrationRunner<AuthAuditDbContext>(scopeFactory),
    ]);
});

builder.Services.AddSingleton<FeatureMigrationRunners>(sp =>
    new FeatureMigrationRunners(
        ModuleMigrations.CreateRunners(isCore: false, sp.GetRequiredService<IServiceScopeFactory>())));

builder.Services.AddSingleton<WorkerRunOutcome>();
builder.Services.AddHostedService<MigrationWorker>();

IHost host = builder.Build();

// Resolve BEFORE RunAsync: RunAsync disposes the host in a finally.
WorkerRunOutcome outcome = host.Services.GetRequiredService<WorkerRunOutcome>();

await host.RunAsync();

return outcome.ExitCode;
