using Wallow.ServiceDefaults;

namespace Wallow.MigrationService;

public sealed partial class MigrationWorker(
    CoreMigrationRunners coreRunners,
    FeatureMigrationRunners featureRunners,
    IHostApplicationLifetime lifetime,
    WorkerRunOutcome outcome,
    ILogger<MigrationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        LogMigrationStarted();

        try
        {
            // Migrate core module contexts and auth audit before feature contexts.
            foreach (IMigrationRunner runner in coreRunners.Runners)
            {
                LogMigratingContext(runner.ContextName);
                await runner.MigrateAsync(stoppingToken);
            }


            LogMigratingFeatureModules();
            await Task.WhenAll(featureRunners.Runners.Select(runner => runner.MigrateAsync(stoppingToken)));

            LogMigrationCompleted();
        }
        catch (Exception ex)
        {
            // Preserve failure for the process exit code so dependent services do not start.
            outcome.MarkFailed();
            LogMigrationFailed(ex);
            throw;
        }
        finally
        {
            lifetime.StopApplication();
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Database migration worker started")]
    private partial void LogMigrationStarted();

    [LoggerMessage(Level = LogLevel.Information, Message = "Migrating {ContextName} database context")]
    private partial void LogMigratingContext(string contextName);

    [LoggerMessage(Level = LogLevel.Information, Message = "Migrating feature module database contexts in parallel")]
    private partial void LogMigratingFeatureModules();

    [LoggerMessage(Level = LogLevel.Information, Message = "All database migrations completed successfully")]
    private partial void LogMigrationCompleted();

    [LoggerMessage(Level = LogLevel.Critical, Message = "Database migration failed")]
    private partial void LogMigrationFailed(Exception ex);
}
