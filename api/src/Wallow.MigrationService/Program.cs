using Microsoft.EntityFrameworkCore;
using Wallow.MigrationService;
using Wallow.ServiceDefaults;
using Wallow.Shared.Infrastructure.Core.Auditing;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

string connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");

// IdentityDbContext requires IDataProtectionProvider
builder.Services.AddDataProtection();

// Auth audit is host-owned and absent from the module registry.
builder.Services.AddDbContext<AuthAuditDbContext>(options =>
    options.UseNpgsql(connectionString, npgsql =>
        npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "auth_audit")));


ModuleMigrations.AddModuleDbContexts(builder.Services, connectionString);

// Core runners finish before feature migrations run in parallel.
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

// Retain the outcome before RunAsync disposes the service provider.
WorkerRunOutcome outcome = host.Services.GetRequiredService<WorkerRunOutcome>();

await host.RunAsync();

return outcome.ExitCode;
