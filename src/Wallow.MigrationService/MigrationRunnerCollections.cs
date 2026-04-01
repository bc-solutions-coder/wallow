namespace Wallow.MigrationService;

public sealed record CoreMigrationRunners(IReadOnlyList<IMigrationRunner> Runners);
public sealed record FeatureMigrationRunners(IReadOnlyList<IMigrationRunner> Runners);
