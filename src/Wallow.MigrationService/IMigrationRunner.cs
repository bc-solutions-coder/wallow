namespace Wallow.MigrationService;

public interface IMigrationRunner
{
    string ContextName { get; }
    Task MigrateAsync(CancellationToken cancellationToken);
}
