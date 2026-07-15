using Microsoft.EntityFrameworkCore;
using Wallow.Announcements.Infrastructure.Persistence;
using Wallow.ApiKeys.Infrastructure.Persistence;
using Wallow.Branding.Infrastructure.Persistence;
using Wallow.Identity.Infrastructure.Persistence;
using Wallow.Inquiries.Infrastructure.Persistence;
using Wallow.MigrationService;
using Wallow.Notifications.Infrastructure.Persistence;
using Wallow.Shared.Infrastructure.Core.Auditing;
using Wallow.Storage.Infrastructure.Persistence;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

string connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");

// IdentityDbContext requires IDataProtectionProvider
builder.Services.AddDataProtection();

// Register all DbContexts for migration
builder.Services.AddDbContext<IdentityDbContext>(options =>
    options.UseNpgsql(connectionString, npgsql =>
        npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "identity")));

builder.Services.AddDbContext<AuditDbContext>(options =>
    options.UseNpgsql(connectionString, npgsql =>
        npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "audit")));

builder.Services.AddDbContext<AuthAuditDbContext>(options =>
    options.UseNpgsql(connectionString, npgsql =>
        npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "auth_audit")));

builder.Services.AddDbContext<BrandingDbContext>(options =>
    options.UseNpgsql(connectionString, npgsql =>
        npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "branding")));

builder.Services.AddDbContext<NotificationsDbContext>(options =>
    options.UseNpgsql(connectionString, npgsql =>
        npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "notifications")));

builder.Services.AddDbContext<AnnouncementsDbContext>(options =>
    options.UseNpgsql(connectionString, npgsql =>
        npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "announcements")));

builder.Services.AddDbContext<StorageDbContext>(options =>
    options.UseNpgsql(connectionString, npgsql =>
        npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "storage")));

builder.Services.AddDbContext<ApiKeysDbContext>(options =>
    options.UseNpgsql(connectionString, npgsql =>
        npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "apikeys")));

builder.Services.AddDbContext<InquiriesDbContext>(options =>
    options.UseNpgsql(connectionString, npgsql =>
        npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "inquiries")));

// Migration runners
builder.Services.AddSingleton<CoreMigrationRunners>(sp => new CoreMigrationRunners(
[
    new DbContextMigrationRunner<IdentityDbContext>(sp.GetRequiredService<IServiceScopeFactory>()),
    new DbContextMigrationRunner<AuditDbContext>(sp.GetRequiredService<IServiceScopeFactory>()),
    new DbContextMigrationRunner<AuthAuditDbContext>(sp.GetRequiredService<IServiceScopeFactory>()),
]));

builder.Services.AddSingleton<FeatureMigrationRunners>(sp => new FeatureMigrationRunners(
[
    new DbContextMigrationRunner<BrandingDbContext>(sp.GetRequiredService<IServiceScopeFactory>()),
    new DbContextMigrationRunner<NotificationsDbContext>(sp.GetRequiredService<IServiceScopeFactory>()),
    new DbContextMigrationRunner<AnnouncementsDbContext>(sp.GetRequiredService<IServiceScopeFactory>()),
    new DbContextMigrationRunner<StorageDbContext>(sp.GetRequiredService<IServiceScopeFactory>()),
    new DbContextMigrationRunner<ApiKeysDbContext>(sp.GetRequiredService<IServiceScopeFactory>()),
    new DbContextMigrationRunner<InquiriesDbContext>(sp.GetRequiredService<IServiceScopeFactory>()),
]));

builder.Services.AddHostedService<MigrationWorker>();

IHost host = builder.Build();
await host.RunAsync();
