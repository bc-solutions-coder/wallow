namespace Wallow.MigrationService;

public sealed partial class MigrationWorker(
    CoreMigrationRunners coreRunners,
    FeatureMigrationRunners featureRunners,
    IHostApplicationLifetime lifetime,
    ILogger<MigrationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        LogMigrationStarted();

        // Core contexts must be migrated first (Identity, Audit, AuthAudit) - sequentially
        foreach (IMigrationRunner runner in coreRunners.Runners)
        {
            LogMigratingContext(runner.ContextName);
            await runner.MigrateAsync(stoppingToken);
        }

        // Feature module contexts can be migrated in parallel
        LogMigratingFeatureModules();
        await Task.WhenAll(featureRunners.Runners.Select(runner => runner.MigrateAsync(stoppingToken)));

        LogMigrationCompleted();
        lifetime.StopApplication();
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Database migration worker started")]
    private partial void LogMigrationStarted();

    [LoggerMessage(Level = LogLevel.Information, Message = "Migrating {ContextName} database context")]
    private partial void LogMigratingContext(string contextName);

    [LoggerMessage(Level = LogLevel.Information, Message = "Migrating feature module database contexts in parallel")]
    private partial void LogMigratingFeatureModules();

    [LoggerMessage(Level = LogLevel.Information, Message = "All database migrations completed successfully")]
    private partial void LogMigrationCompleted();
}
