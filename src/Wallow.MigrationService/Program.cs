using Wallow.Announcements.Infrastructure.Persistence;
using Wallow.ApiKeys.Infrastructure.Persistence;
using Wallow.Branding.Infrastructure.Persistence;
using Wallow.Identity.Infrastructure.Persistence;
using Wallow.Inquiries.Infrastructure.Persistence;
using Wallow.MigrationService;
using Wallow.Notifications.Infrastructure.Persistence;
using Wallow.Shared.Infrastructure.Core.Auditing;
using Wallow.Storage.Infrastructure.Persistence;

IHostBuilder builder = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddSingleton<CoreMigrationRunners>(sp => new CoreMigrationRunners(
        [
            new DbContextMigrationRunner<IdentityDbContext>(sp.GetRequiredService<IServiceScopeFactory>()),
            new DbContextMigrationRunner<AuditDbContext>(sp.GetRequiredService<IServiceScopeFactory>()),
            new DbContextMigrationRunner<AuthAuditDbContext>(sp.GetRequiredService<IServiceScopeFactory>()),
        ]));

        services.AddSingleton<FeatureMigrationRunners>(sp => new FeatureMigrationRunners(
        [
            new DbContextMigrationRunner<BrandingDbContext>(sp.GetRequiredService<IServiceScopeFactory>()),
            new DbContextMigrationRunner<NotificationsDbContext>(sp.GetRequiredService<IServiceScopeFactory>()),
            new DbContextMigrationRunner<AnnouncementsDbContext>(sp.GetRequiredService<IServiceScopeFactory>()),
            new DbContextMigrationRunner<StorageDbContext>(sp.GetRequiredService<IServiceScopeFactory>()),
            new DbContextMigrationRunner<ApiKeysDbContext>(sp.GetRequiredService<IServiceScopeFactory>()),
            new DbContextMigrationRunner<InquiriesDbContext>(sp.GetRequiredService<IServiceScopeFactory>()),
        ]));

        services.AddHostedService<MigrationWorker>();
    });

IHost host = builder.Build();
await host.RunAsync();
