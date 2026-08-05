using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Wallow.Modules.Registry;
using Wallow.Shared.Infrastructure.Modules;

namespace Wallow.MigrationService;

/// <summary>
/// The module registry as the migration host sees it: every module's own
/// <see cref="IWallowModule.DbContextTypes"/> and <see cref="IWallowModule.SchemaName"/> drive both
/// the <c>AddDbContext</c> registrations and the migration runners, so adding a module no longer
/// means editing a hand-maintained list of contexts and schema strings here.
/// </summary>
/// <remarks>
/// The migration host does NOT honour feature flags — it migrates every module's schema whether or
/// not the API will register that module, because a disabled module must still be able to come back
/// on without a migration step. That is why this list is unfiltered while the API's equivalent is
/// filtered through <c>IFeatureManager</c>.
/// </remarks>
internal static class ModuleMigrations
{
    /// <summary>
    /// The generic method reflection targets. It is this class's own private method rather than EF
    /// Core's <c>AddDbContext</c> overload set, so the lookup cannot be broken by EF adding or
    /// reordering overloads.
    /// </summary>
    private static readonly MethodInfo _addSchemaScopedDbContext =
        typeof(ModuleMigrations).GetMethod(
            nameof(AddSchemaScopedDbContext),
            BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException($"{nameof(AddSchemaScopedDbContext)} was not found.");

    /// <summary>
    /// Gets every module this host migrates. Identity is <see cref="IWallowModule.IsCore"/> and so
    /// lands in the core runner group; the other six are feature modules.
    /// </summary>
    /// <remarks>
    /// This is the same list <c>Wallow.Api.WallowModules</c> reads — <see cref="WallowModuleRegistry"/>
    /// exists so neither host has to keep its own copy. The difference between the hosts is the
    /// filtering described above, not the membership: this host takes the registry whole.
    /// </remarks>
    public static IReadOnlyList<IWallowModule> All => WallowModuleRegistry.All;

    /// <summary>
    /// Registers every module-owned <see cref="DbContext"/> against the module's own schema.
    /// </summary>
    /// <param name="services">The service collection to register into.</param>
    /// <param name="connectionString">The database connection string all contexts share.</param>
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
    /// Builds one <see cref="IMigrationRunner"/> per <see cref="DbContext"/> owned by the modules
    /// matching <paramref name="isCore"/>.
    /// </summary>
    /// <param name="isCore">
    /// <see langword="true"/> for the core group, <see langword="false"/> for feature modules. The
    /// split matters: Identity is a module AND core, so selecting on this rather than on "not
    /// Identity" is what keeps <c>IdentityDbContext</c> from being migrated twice.
    /// </param>
    /// <param name="scopeFactory">The scope factory each runner resolves its context from.</param>
    /// <returns>The runners, in module registration order.</returns>
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
