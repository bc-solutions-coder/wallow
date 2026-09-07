using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Wallow.Modules.Registry;
using Wallow.Shared.Infrastructure.Modules;

namespace Wallow.MigrationService;

/// <summary>
/// Registers migration contexts and runners for every shipped module, including disabled API modules.
/// </summary>
internal static class ModuleMigrations
{
    /// <summary>
    /// Reflect over the local generic wrapper instead of selecting an EF registration overload.
    /// </summary>
    private static readonly MethodInfo _addSchemaScopedDbContext =
        typeof(ModuleMigrations).GetMethod(
            nameof(AddSchemaScopedDbContext),
            BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException($"{nameof(AddSchemaScopedDbContext)} was not found.");

    /// <summary>
    /// All shipped modules, without API feature filtering.
    /// </summary>
    public static IReadOnlyList<IWallowModule> All => WallowModuleRegistry.All;

    /// <summary>
    /// Registers module contexts with their declared migration-history schema.
    /// </summary>
    public static void AddModuleDbContexts(IServiceCollection services, string connectionString)
    {
        foreach (IWallowModule module in All)
        {
            foreach (Type contextType in module.DbContextTypes)
            {
                _addSchemaScopedDbContext
                    .MakeGenericMethod(EnsureDbContextType(module, contextType))
                    .Invoke(null, [services, connectionString, module.SchemaName]);
            }
        }
    }

    /// <summary>
    /// Builds one migration runner per context for modules matching the core flag.
    /// </summary>
    public static IReadOnlyList<IMigrationRunner> CreateRunners(bool isCore, IServiceScopeFactory scopeFactory)
    {
        return
        [
            .. All
                .Where(module => module.IsCore == isCore)
                .SelectMany(module => module.DbContextTypes.Select(
                    contextType => CreateRunner(EnsureDbContextType(module, contextType), scopeFactory)))
        ];
    }

    private static IMigrationRunner CreateRunner(Type contextType, IServiceScopeFactory scopeFactory)
    {
        Type runnerType = typeof(DbContextMigrationRunner<>).MakeGenericType(contextType);
        return (IMigrationRunner)Activator.CreateInstance(runnerType, scopeFactory)!;
    }

    private static Type EnsureDbContextType(IWallowModule module, Type contextType)
    {
        if (!typeof(DbContext).IsAssignableFrom(contextType))
        {
            throw new InvalidOperationException(
                $"Module '{module.Name}' declared {contextType.FullName} in DbContextTypes, but it does not derive from DbContext.");
        }

        return contextType;
    }

    private static void AddSchemaScopedDbContext<TContext>(
        IServiceCollection services,
        string connectionString,
        string schemaName)
        where TContext : DbContext
    {
        services.AddDbContext<TContext>(options =>
            options.UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", schemaName)));
    }
}
