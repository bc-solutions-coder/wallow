using Microsoft.EntityFrameworkCore;

namespace Wallow.MigrationService;

public sealed class DbContextMigrationRunner<TContext>(IServiceScopeFactory scopeFactory) : IMigrationRunner
    where TContext : DbContext
{
    public string ContextName { get; } = typeof(TContext).Name.Replace("DbContext", string.Empty, StringComparison.Ordinal);

    public async Task MigrateAsync(CancellationToken cancellationToken)
    {
        using IServiceScope scope = scopeFactory.CreateScope();
        TContext dbContext = scope.ServiceProvider.GetRequiredService<TContext>();
        await dbContext.Database.MigrateAsync(cancellationToken);
    }
}
