using System.Reflection;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Wallow.ApiKeys.Infrastructure.Persistence;
using Wallow.Identity.Infrastructure.Persistence;
using Wallow.Shared.Infrastructure.Core.Auditing;
using Wallow.Shared.Infrastructure.Modules;
using Wallow.Tests.Common.Factories;

namespace Wallow.Api.Tests.Integration;

/// <summary>
/// Checks migration of supplied probe modules and the host-owned auth-audit context.
/// </summary>
[Collection(nameof(ApiIntegrationTestCollection))]
[Trait("Category", "Integration")]
public sealed class RunTestMigrationsTests : IDisposable
{
    private readonly WallowApiFactory _factory;
    private readonly ServiceProvider _provider;

    public RunTestMigrationsTests(WallowApiFactory factory)
    {
        _factory = factory;

        // Build the Testing host before reading its container connection string.
        string connectionString = factory.Services.GetRequiredService<IConfiguration>()
            .GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("The test host has no DefaultConnection.");

        ServiceCollection services = new();

        // Identity persistence requires data protection.
        services.AddDataProtection();

        // Register Identity alongside the host-owned and probe contexts.
        AddSchemaScopedDbContext<IdentityDbContext>(services, connectionString, "identity");
        AddSchemaScopedDbContext<AuthAuditDbContext>(services, connectionString, "auth_audit");
        AddSchemaScopedDbContext<ProbeFeatureDbContext>(services, connectionString, ProbeFeatureDbContext.Schema);
        AddSchemaScopedDbContext<ProbeCoreDbContext>(services, connectionString, ProbeCoreDbContext.Schema);

        _provider = services.BuildServiceProvider();
    }

    [Fact]
    public async Task RunTestMigrationsAsync_MigratesTheContextOfAFeatureModuleItDoesNotNameItself()
    {
        await WallowModules.RunTestMigrationsAsync(_provider, [new ProbeFeatureModule()]);

        IEnumerable<string> applied = await GetAppliedMigrationsAsync<ProbeFeatureDbContext>();

        applied.Should().Contain(
            ProbeFeatureMigration.Id,
            "a feature module handed to RunTestMigrationsAsync must have its declared DbContext migrated, "
            + "whether or not the method mentions that context by name");
    }

    [Fact]
    public async Task RunTestMigrationsAsync_MigratesTheContextOfACoreModuleItDoesNotNameItself()
    {
        await WallowModules.RunTestMigrationsAsync(_provider, [new ProbeCoreModule()]);

        IEnumerable<string> applied = await GetAppliedMigrationsAsync<ProbeCoreDbContext>();

        applied.Should().Contain(
            ProbeCoreMigration.Id,
            "a core module handed to RunTestMigrationsAsync must have its declared DbContext migrated too — "
            + "the core/feature split must not become a place where a module is dropped");
    }

    [Fact]
    public async Task RunTestMigrationsAsync_MigratesTheHostOwnedAuthAuditContext_WhichNoModuleDeclares()
    {
        // The supplied module does not declare AuthAuditDbContext.
        await WallowModules.RunTestMigrationsAsync(_provider, [new ProbeFeatureModule()]);

        (await GetAppliedMigrationsAsync<AuthAuditDbContext>()).Should().NotBeEmpty(
            "AuthAuditDbContext belongs to no module and must still be migrated");
    }

    [Fact]
    public async Task TestHostBoot_MigratesTheHostOwnedAuthAuditContext()
    {

        await using AsyncServiceScope scope = _factory.Services.CreateAsyncScope();

        IEnumerable<string> authAudit = await scope.ServiceProvider
            .GetRequiredService<AuthAuditDbContext>().Database.GetAppliedMigrationsAsync();

        authAudit.Should().NotBeEmpty("the Testing host boot is the only thing that migrates this database");
    }

    [Fact]
    public async Task TestHostBoot_MigratesAModuleThatIsEnabledOnlyByConfiguration()
    {
        // Enable the normally disabled module before service registration reads the flag.
        using WebApplicationFactory<Program> apiKeysEnabled = _factory.WithWebHostBuilder(builder =>
            builder.UseSetting("FeatureManagement:Modules.ApiKeys", "true"));

        await using AsyncServiceScope scope = apiKeysEnabled.Services.CreateAsyncScope();
        ApiKeysDbContext context = scope.ServiceProvider.GetRequiredService<ApiKeysDbContext>();

        IEnumerable<string> applied = await context.Database.GetAppliedMigrationsAsync();

        applied.Should().NotBeEmpty(
            "a module enabled by configuration is part of the enabled set and must be migrated with it");
    }

    public void Dispose()
    {
        _provider.Dispose();
    }

    private async Task<IEnumerable<string>> GetAppliedMigrationsAsync<TContext>()
        where TContext : DbContext
    {
        await using AsyncServiceScope scope = _provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<TContext>().Database.GetAppliedMigrationsAsync();
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

/// <summary>A feature module that exists only inside this test assembly.</summary>
public sealed class ProbeFeatureModule : IWallowModule
{
    public string Name => "MigrationProbeFeature";

    public bool IsCore => false;

    public IReadOnlyList<Assembly> HandlerAssemblies => [];

    public IReadOnlyList<Type> DbContextTypes => [typeof(ProbeFeatureDbContext)];

    public string SchemaName => ProbeFeatureDbContext.Schema;

    public IServiceCollection AddServices(
        IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment) => services;
}

/// <summary>A core module that exists only inside this test assembly.</summary>
public sealed class ProbeCoreModule : IWallowModule
{
    public string Name => "MigrationProbeCore";

    public bool IsCore => true;

    public IReadOnlyList<Assembly> HandlerAssemblies => [];

    public IReadOnlyList<Type> DbContextTypes => [typeof(ProbeCoreDbContext)];

    public string SchemaName => ProbeCoreDbContext.Schema;

    public IServiceCollection AddServices(
        IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment) => services;
}

/// <summary>
/// Empty model used to observe migration application through the history table.
/// </summary>
public sealed class ProbeFeatureDbContext(DbContextOptions<ProbeFeatureDbContext> options) : DbContext(options)
{
    public const string Schema = "migration_probe_feature";
}

/// <inheritdoc cref="ProbeFeatureDbContext"/>
public sealed class ProbeCoreDbContext(DbContextOptions<ProbeCoreDbContext> options) : DbContext(options)
{
    public const string Schema = "migration_probe_core";
}

/// <summary>
/// Creates a probe marker table; assertions observe the migration history entry.
/// </summary>
[DbContext(typeof(ProbeFeatureDbContext))]
[Migration(Id)]
public sealed class ProbeFeatureMigration : Migration
{
    public const string Id = "20260805000000_ProbeFeatureInitial";

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            $"""CREATE TABLE IF NOT EXISTS "{ProbeFeatureDbContext.Schema}"."probe_marker" (id uuid PRIMARY KEY);""");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql($"""DROP TABLE IF EXISTS "{ProbeFeatureDbContext.Schema}"."probe_marker";""");
    }
}

/// <inheritdoc cref="ProbeFeatureMigration"/>
[DbContext(typeof(ProbeCoreDbContext))]
[Migration(Id)]
public sealed class ProbeCoreMigration : Migration
{
    public const string Id = "20260805000000_ProbeCoreInitial";

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            $"""CREATE TABLE IF NOT EXISTS "{ProbeCoreDbContext.Schema}"."probe_marker" (id uuid PRIMARY KEY);""");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql($"""DROP TABLE IF EXISTS "{ProbeCoreDbContext.Schema}"."probe_marker";""");
    }
}
