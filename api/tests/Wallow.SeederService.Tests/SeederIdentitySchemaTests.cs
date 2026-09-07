using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Wallow.Identity.Infrastructure.Modules;
using Wallow.Identity.Infrastructure.Persistence;

namespace Wallow.SeederService.Tests;

/// <summary>
/// Checks that the seeder model and migration-history SQL use the Identity module schema, without opening a connection.
/// </summary>
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
