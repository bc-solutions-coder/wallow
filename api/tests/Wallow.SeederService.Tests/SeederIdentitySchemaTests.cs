using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Wallow.Identity.Infrastructure.Modules;
using Wallow.Identity.Infrastructure.Persistence;

namespace Wallow.SeederService.Tests;

/// <summary>
/// The seeder is the third host that builds <see cref="IdentityDbContext"/>, and the only one that
/// lives outside <c>Wallow.Identity.Infrastructure</c>. The API host and the migration host are
/// guarded together in <c>Wallow.Architecture.Tests</c>'s <c>ModuleSchemaTests</c>; that suite
/// cannot see this container, so the cross-assembly leg is pinned here.
/// </summary>
/// <remarks>
/// This matters because the accessibility that keeps the other three declaration sites honest — a
/// constant the compiler resolves inside the module's own assembly — is exactly what a separate
/// assembly cannot rely on by convention alone. If someone hand-types <c>"identity"</c> back into
/// <c>AddSeederIdentityServices</c>, or the module renames
/// its schema without the seeder following, this test fails. No connection is opened: EF generates
/// the history-table script in memory.
/// </remarks>
public class SeederIdentitySchemaTests
{
    private const string ConnectionString =
        "Host=localhost;Port=5432;Database=seeder_schema_test;Username=test;Password=test";

    [Fact]
    public void AddSeederIdentityServices_ShouldPlaceMigrationsHistory_InTheSchemaTheIdentityModuleDeclares()
    {
        IdentityModule identityModule = new();

        using ServiceProvider provider = BuildProvider();
        using IServiceScope scope = provider.CreateScope();
        IdentityDbContext context = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();

        string createScript = context.GetService<IHistoryRepository>().GetCreateScript();

        createScript.Should().Contain(
            $"{identityModule.SchemaName}.\"__EFMigrationsHistory\"",
            "the seeder shares one physical database with the migration host, so it must agree with "
            + "the Identity module's own declared schema rather than carry its own copy of the string");
    }

    [Fact]
    public void AddSeederIdentityServices_ShouldReadAndWrite_TheSchemaTheIdentityModuleDeclares()
    {
        IdentityModule identityModule = new();

        using ServiceProvider provider = BuildProvider();
        using IServiceScope scope = provider.CreateScope();
        IdentityDbContext context = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();

        context.Model.GetDefaultSchema().Should().Be(
            identityModule.SchemaName,
            "the rows the seeder writes must land in the schema the migration host created the "
            + "tables in");
    }

    private static ServiceProvider BuildProvider()
    {
        ServiceCollection services = new();
        services.AddLogging();

        IConfiguration configuration = new ConfigurationBuilder().Build();
        services.AddSeederIdentityServices(configuration, ConnectionString);

        return services.BuildServiceProvider();
    }
}
