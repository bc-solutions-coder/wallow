using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Wallow.Identity.Application.Commands.BootstrapAdmin;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Application.Queries.IsSetupRequired;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Infrastructure.Services;

namespace Wallow.SeederService.Tests;

/// <summary>
/// Resolves the worker service set through the production seeder registration method to catch missing dependencies such as <see cref="ILastOwnerGuard"/>.
/// </summary>
public class SeederIdentityServiceRegistrationTests
{
    private const string ConnectionString = "Host=localhost;Port=5432;Database=seeder_di_test;Username=test;Password=test";

    [Fact]
    public void AddSeederIdentityServices_ResolvesOrganizationSeedSyncService()
    {
        using ServiceProvider provider = BuildProvider();
        using IServiceScope scope = provider.CreateScope();

        Action resolve = () => scope.ServiceProvider.GetRequiredService<OrganizationSeedSyncService>();

        // Resolve the sync service to exercise its transitive organization-service dependencies.
        resolve.Should().NotThrow(
            "SeederWorker.SyncOrganizationsAsync must be able to construct OrganizationSeedSyncService " +
            "and, transitively, OrganizationService, from the seeder's own container");
    }

    [Theory]
    [MemberData(nameof(ServicesSeederWorkerResolvesDirectly))]
    public void AddSeederIdentityServices_ResolvesEveryServiceSeederWorkerRequestsFromItsScope(Type serviceType)
    {
        using ServiceProvider provider = BuildProvider();
        using IServiceScope scope = provider.CreateScope();

        Action resolve = () => scope.ServiceProvider.GetRequiredService(serviceType);

        resolve.Should().NotThrow(
            $"SeederWorker.ExecuteAsync calls sp.GetRequiredService<{serviceType.Name}>() " +
            "against this exact container while running its seed steps in order");
    }

    public static TheoryData<Type> ServicesSeederWorkerResolvesDirectly => new()
    {
        typeof(RoleManager<WallowRole>),
        typeof(OpenIddictScopeSyncService),
        typeof(ISetupStatusChecker),
        typeof(IBootstrapAdminService),
        typeof(BootstrapAdminHandler),
        typeof(OrganizationSeedSyncService),
        typeof(PreRegisteredClientSyncService),
    };

    private static ServiceProvider BuildProvider()
    {
        ServiceCollection services = new();
        services.AddLogging();

        IConfiguration configuration = new ConfigurationBuilder().Build();
        services.AddSeederIdentityServices(configuration, ConnectionString);

        return services.BuildServiceProvider();
    }
}
