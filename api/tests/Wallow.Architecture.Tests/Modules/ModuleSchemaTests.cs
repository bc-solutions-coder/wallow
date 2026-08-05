using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;
using NSubstitute;
using StackExchange.Redis;
using Wallow.Modules.Registry;
using Wallow.Shared.Infrastructure.Modules;
using Wallow.Shared.Kernel.Extensions;

namespace Wallow.Architecture.Tests.Modules;

/// <summary>
/// Guards the "one schema per module, declared once" property across every host that builds a
/// module's <see cref="DbContext"/>.
/// </summary>
/// <remarks>
/// <para>
/// The expectation these tests compare against is deliberately NOT <see cref="IWallowModule.SchemaName"/>.
/// It is read out of the module's own checked-in EF migrations — the generated
/// <c>Migrations/*_InitialCreate.cs</c> operations, whose <c>schema:</c> arguments were baked in by
/// <c>dotnet ef migrations add</c> and are independent of any constant the module source declares
/// today. Those operations are what actually create the tables in Postgres, so they are the ground
/// truth for "where this module's data lives"; everything else (the module's declared
/// <c>SchemaName</c>, each host's <c>MigrationsHistoryTable</c>, the model's default schema) is
/// checked against them rather than against each other. A guard that only compared the hosts to
/// <c>SchemaName</c> would pass happily if a schema were renamed everywhere in source while the
/// committed migrations still created the old one.
/// </para>
/// <para>
/// Nothing here opens a database connection: the connection string is syntactically valid and
/// unreachable, EF builds its model, its migration operations and its history-table script entirely
/// in memory, and no test calls <c>Migrate</c>, <c>EnsureCreated</c> or any query.
/// </para>
/// </remarks>
public class ModuleSchemaTests : IClassFixture<ModuleSchemaHostsFixture>
{
    private readonly ModuleSchemaHostsFixture _hosts;

    public ModuleSchemaTests(ModuleSchemaHostsFixture hosts)
    {
        _hosts = hosts;
    }

    public static TheoryData<string> ModuleNames
    {
        get
        {
            TheoryData<string> data = new();
            foreach (IWallowModule module in WallowModuleRegistry.All)
            {
                data.Add(module.Name);
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(ModuleNames))]
    public void Migrations_ShouldCreateEverything_InExactlyOneSchema(string moduleName)
    {
        IWallowModule module = ModuleNamed(moduleName);

        foreach (Type contextType in module.DbContextTypes)
        {
            using ModuleDbContextLease lease = _hosts.CreateMigrationHostContext(contextType);
            IReadOnlyList<string> schemas = SchemasNamedByMigrations(lease.Context);

            schemas.Should().ContainSingle(
                $"{contextType.Name}'s committed migrations must create every object in the one "
                + "schema the module owns — a second schema here means the module's tables are "
                + "being split across schemas, and no single SchemaName can describe it");
        }
    }

    [Theory]
    [MemberData(nameof(ModuleNames))]
    public void DeclaredSchemaName_ShouldBe_TheSchemaItsMigrationsCreate(string moduleName)
    {
        IWallowModule module = ModuleNamed(moduleName);

        foreach (Type contextType in module.DbContextTypes)
        {
            using ModuleDbContextLease lease = _hosts.CreateMigrationHostContext(contextType);
            string migratedSchema = SoleSchemaNamedByMigrations(lease.Context);

            module.SchemaName.Should().Be(
                migratedSchema,
                $"the migration host hands {nameof(IWallowModule)}.{nameof(IWallowModule.SchemaName)} "
                + "straight to MigrationsHistoryTable, so declaring a schema the module's own "
                + "migrations do not create records the history rows away from the tables they "
                + "describe");
        }
    }

    [Theory]
    [MemberData(nameof(ModuleNames))]
    public void ApiHost_ShouldPlaceDataAndMigrationsHistory_InTheSchemaTheMigrationsCreate(string moduleName)
    {
        IWallowModule module = ModuleNamed(moduleName);

        foreach (Type contextType in module.DbContextTypes)
        {
            string migratedSchema;
            using (ModuleDbContextLease migrationHost = _hosts.CreateMigrationHostContext(contextType))
            {
                migratedSchema = SoleSchemaNamedByMigrations(migrationHost.Context);
            }

            using (ModuleDbContextLease apiHost = _hosts.CreateApiHostContext(contextType))
            {
                apiHost.Context.Model.GetDefaultSchema().Should().Be(
                    migratedSchema,
                    $"the API host's {contextType.Name} must read and write the tables its migrations created");

                MigrationsHistorySchemaOf(apiHost.Context).Should().Be(
                    migratedSchema,
                    "the API host applies these same migrations inline in the Testing environment, "
                    + "so it must look for __EFMigrationsHistory where the migration host wrote it — "
                    + "otherwise it replays migrations against tables that already exist");
            }
        }
    }

    [Theory]
    [MemberData(nameof(ModuleNames))]
    public void MigrationHost_ShouldPlaceDataAndMigrationsHistory_InTheSchemaTheMigrationsCreate(string moduleName)
    {
        IWallowModule module = ModuleNamed(moduleName);

        foreach (Type contextType in module.DbContextTypes)
        {
            using ModuleDbContextLease lease = _hosts.CreateMigrationHostContext(contextType);
            string migratedSchema = SoleSchemaNamedByMigrations(lease.Context);

            lease.Context.Model.GetDefaultSchema().Should().Be(migratedSchema);

            MigrationsHistorySchemaOf(lease.Context).Should().Be(
                migratedSchema,
                "Wallow.MigrationService is the host that actually applies these migrations outside "
                + "Testing, so its history table must live beside the tables it creates");
        }
    }

    [Theory]
    [MemberData(nameof(ModuleNames))]
    public void BothHosts_ShouldAgreeOn_DefaultSchemaAndMigrationsHistorySchema(string moduleName)
    {
        IWallowModule module = ModuleNamed(moduleName);

        foreach (Type contextType in module.DbContextTypes)
        {
            using (ModuleDbContextLease apiHost = _hosts.CreateApiHostContext(contextType))
            using (ModuleDbContextLease migrationHost = _hosts.CreateMigrationHostContext(contextType))
            {
                apiHost.Context.Model.GetDefaultSchema().Should().Be(
                    migrationHost.Context.Model.GetDefaultSchema(),
                    "both hosts build the same compiled DbContext type, so a difference here means "
                    + "one of them was handed a different schema");

                MigrationsHistorySchemaOf(apiHost.Context).Should().Be(
                    MigrationsHistorySchemaOf(migrationHost.Context),
                    "the two hosts share one physical database; if they disagree about where "
                    + "__EFMigrationsHistory lives they each see the other's work as unapplied");
            }
        }
    }

    private static IWallowModule ModuleNamed(string moduleName)
    {
        return WallowModuleRegistry.All.Single(module => module.Name == moduleName);
    }

    /// <summary>
    /// Reads the schema Npgsql will actually name in the <c>CREATE TABLE</c> it emits for
    /// <c>__EFMigrationsHistory</c>. <see cref="IHistoryRepository.GetCreateScript"/> is pure SQL
    /// generation — it opens no connection — which is what lets this suite stay in the fast tier.
    /// </summary>
    private static string MigrationsHistorySchemaOf(DbContext context)
    {
        string createScript = context.GetService<IHistoryRepository>().GetCreateScript();

        return SchemaQualifierIn(createScript);
    }

    /// <summary>
    /// Pulls the schema out of <c>CREATE TABLE IF NOT EXISTS &lt;schema&gt;."__EFMigrationsHistory"</c>,
    /// tolerating either quoted or bare identifiers.
    /// </summary>
    private static string SchemaQualifierIn(string createScript)
    {
        const string historyTable = "\"__EFMigrationsHistory\"";

        int tableIndex = createScript.IndexOf(historyTable, StringComparison.Ordinal);
        tableIndex.Should().BeGreaterThan(
            0,
            "the history-table create script must name __EFMigrationsHistory");

        string beforeTable = createScript[..tableIndex];
        int dotIndex = beforeTable.LastIndexOf('.');
        dotIndex.Should().BeGreaterThan(
            0,
            "the history table must be schema-qualified — an unqualified name lands in whatever "
            + "the connection's search_path happens to be");

        int start = beforeTable.LastIndexOfAny([' ', '\n', '\r', '\t'], dotIndex) + 1;

        return beforeTable[start..dotIndex].Trim('"');
    }

    /// <summary>
    /// The distinct schemas the module's committed migrations name, across every operation in every
    /// migration — including the ones nested inside a <see cref="CreateTableOperation"/>.
    /// </summary>
    private static IReadOnlyList<string> SchemasNamedByMigrations(DbContext context)
    {
        IMigrationsAssembly migrationsAssembly = context.GetService<IMigrationsAssembly>();
        string activeProvider = context.Database.ProviderName
            ?? throw new InvalidOperationException("The context has no database provider.");

        SortedSet<string> schemas = new(StringComparer.Ordinal);

        foreach (KeyValuePair<string, TypeInfo> entry in migrationsAssembly.Migrations)
        {
            Migration migration = migrationsAssembly.CreateMigration(entry.Value, activeProvider);

            foreach (MigrationOperation operation in migration.UpOperations)
            {
                CollectSchemas(operation, schemas);
            }
        }

        return [.. schemas];
    }

    private static string SoleSchemaNamedByMigrations(DbContext context)
    {
        IReadOnlyList<string> schemas = SchemasNamedByMigrations(context);

        schemas.Should().ContainSingle(
            "this module's migrations must create every object in exactly one schema");

        return schemas[0];
    }

    private static void CollectSchemas(MigrationOperation operation, ISet<string> schemas)
    {
        if (operation is EnsureSchemaOperation ensureSchema)
        {
            schemas.Add(ensureSchema.Name);
        }

        foreach (PropertyInfo property in operation.GetType()
                     .GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.PropertyType == typeof(string)
                && property.Name is "Schema" or "PrincipalSchema"
                && property.GetValue(operation) is string schema
                && schema.Length > 0)
            {
                schemas.Add(schema);
            }
        }

        if (operation is CreateTableOperation createTable)
        {
            foreach (MigrationOperation nested in NestedOperationsOf(createTable))
            {
                CollectSchemas(nested, schemas);
            }
        }
    }

    private static IEnumerable<MigrationOperation> NestedOperationsOf(CreateTableOperation createTable)
    {
        foreach (AddColumnOperation column in createTable.Columns)
        {
            yield return column;
        }

        if (createTable.PrimaryKey is not null)
        {
            yield return createTable.PrimaryKey;
        }

        foreach (AddForeignKeyOperation foreignKey in createTable.ForeignKeys)
        {
            yield return foreignKey;
        }

        foreach (AddUniqueConstraintOperation uniqueConstraint in createTable.UniqueConstraints)
        {
            yield return uniqueConstraint;
        }

        foreach (AddCheckConstraintOperation checkConstraint in createTable.CheckConstraints)
        {
            yield return checkConstraint;
        }
    }
}

/// <summary>
/// A module <see cref="DbContext"/> plus whatever owns its lifetime in the host that produced it —
/// the context itself for a pooled factory, the enclosing scope for a scoped registration.
/// </summary>
public sealed class ModuleDbContextLease : IDisposable
{
    private readonly IDisposable _owner;

    public ModuleDbContextLease(DbContext context, IDisposable owner)
    {
        Context = context;
        _owner = owner;
    }

    public DbContext Context { get; }

    public void Dispose()
    {
        _owner.Dispose();
    }
}

/// <summary>
/// Builds the API host's and the migration host's real containers once for the whole suite, each
/// through that host's own production registration code rather than a hand-rolled
/// <see cref="DbContextOptions"/> — building the options by hand would test the test, not the host.
/// </summary>
public sealed class ModuleSchemaHostsFixture : IDisposable
{
    /// <summary>
    /// Syntactically valid and deliberately unreachable. Nothing in this suite connects; the same
    /// string is already used by <c>ModuleRegistryTests</c> for the same reason.
    /// </summary>
    private const string UnreachableConnectionString = "Host=localhost;Database=test";

    private static readonly Dictionary<string, string?> _allModuleFlagsEnabled = new()
    {
        ["FeatureManagement:Modules.Identity"] = "true",
        ["FeatureManagement:Modules.Branding"] = "true",
        ["FeatureManagement:Modules.Notifications"] = "true",
        ["FeatureManagement:Modules.Announcements"] = "true",
        ["FeatureManagement:Modules.Storage"] = "true",
        ["FeatureManagement:Modules.ApiKeys"] = "true",
        ["FeatureManagement:Modules.Inquiries"] = "true",
        ["ConnectionStrings:DefaultConnection"] = UnreachableConnectionString,
    };

    private readonly ServiceProvider _apiHost;
    private readonly ServiceProvider _migrationHost;

    public ModuleSchemaHostsFixture()
    {
        _apiHost = BuildApiHost();
        _migrationHost = BuildMigrationHost();
    }

    /// <summary>
    /// Creates the context the API host would use, out of the module's own
    /// <c>AddPooledDbContextFactory</c> registration.
    /// </summary>
    public ModuleDbContextLease CreateApiHostContext(Type contextType)
    {
        Type factoryType = typeof(IDbContextFactory<>).MakeGenericType(contextType);
        object factory = _apiHost.GetRequiredService(factoryType);
        MethodInfo createDbContext = factoryType.GetMethod("CreateDbContext")
            ?? throw new InvalidOperationException($"{factoryType.Name} has no CreateDbContext method.");

        DbContext context = (DbContext)createDbContext.Invoke(factory, null)!;

        // The pooled factory hands out a context the caller owns; returning it to the pool is what
        // disposing it means here.
        return new ModuleDbContextLease(context, context);
    }

    /// <summary>
    /// Creates the context the migration host would use, out of
    /// <c>ModuleMigrations.AddModuleDbContexts</c>'s schema-scoped registration.
    /// </summary>
    /// <remarks>
    /// <c>AddModuleDbContexts</c> registers scoped contexts, and a scoped context is owned by its
    /// scope — disposing the context itself would poison the container for every later caller. The
    /// scope is what the lease disposes.
    /// </remarks>
    public ModuleDbContextLease CreateMigrationHostContext(Type contextType)
    {
        IServiceScope scope = _migrationHost.CreateScope();
        DbContext context = (DbContext)scope.ServiceProvider.GetRequiredService(contextType);

        return new ModuleDbContextLease(context, scope);
    }

    public void Dispose()
    {
        _apiHost.Dispose();
        _migrationHost.Dispose();
    }

    private static ServiceProvider BuildApiHost()
    {
        ServiceCollection services = new();

        // Modules resolve IConnectionMultiplexer at registration time for Redis-backed services.
        IConnectionMultiplexer mockRedis = Substitute.For<IConnectionMultiplexer>();
        services.AddSingleton(mockRedis);

        // Program.cs registers the shared kernel before the modules; six of the seven module
        // DbContext registrations resolve TenantSaveChangesInterceptor out of it while building
        // their DbContextOptions.
        services.AddSharedKernel();

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(_allModuleFlagsEnabled)
            .Build();

        Assembly apiAssembly = Assembly.Load("Wallow.Api");
        Type wallowModulesType = apiAssembly.GetType("Wallow.Api.WallowModules")!;
        MethodInfo addMethod = wallowModulesType.GetMethod(
            "AddWallowModules", BindingFlags.Public | BindingFlags.Static)!;
        IHostEnvironment environment = new HostingEnvironment { EnvironmentName = Environments.Development };

        addMethod.Invoke(null, [services, configuration, environment]);

        return services.BuildServiceProvider();
    }

    private static ServiceProvider BuildMigrationHost()
    {
        ServiceCollection services = new();

        // Program.cs registers this before the module contexts; IdentityDbContext's constructor
        // takes an IDataProtectionProvider.
        services.AddDataProtection();

        Assembly migrationAssembly = Assembly.Load("Wallow.MigrationService");
        Type moduleMigrationsType = migrationAssembly.GetType("Wallow.MigrationService.ModuleMigrations")!;
        MethodInfo addModuleDbContexts = moduleMigrationsType.GetMethod(
            "AddModuleDbContexts", BindingFlags.Public | BindingFlags.Static)!;

        addModuleDbContexts.Invoke(null, [services, UnreachableConnectionString]);

        return services.BuildServiceProvider();
    }
}
