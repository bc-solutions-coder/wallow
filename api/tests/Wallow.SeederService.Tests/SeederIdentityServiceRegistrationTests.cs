using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Wallow.Identity.Application.Commands.BootstrapAdmin;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Infrastructure.Services;

namespace Wallow.SeederService.Tests;

/// <summary>
/// Wallow-smvc: commit fa80e5ff added <see cref="ILastOwnerGuard"/> as a constructor parameter of
/// <c>OrganizationService</c> and registered it in the Identity module's own
/// <c>IdentityInfrastructureExtensions</c>, but the seeder hand-picks its own DI subset
/// (<see cref="SeederServiceCollectionExtensions.AddSeederIdentityServices"/>) instead of calling
/// that extension, and was never updated to register it too. The result: every step of
/// <see cref="SeederWorker.ExecuteAsync"/> up to and including "Sync Organizations" threw at DI
/// construction time, and everything after it (including seeding every OpenIddict client) silently
/// never ran.
///
/// These tests build the SAME container Program.cs builds — through the shared, production
/// extension method, not a duplicated registration list — and resolve each service
/// <see cref="SeederWorker"/> asks its scope for, in the same order it asks for them. A test that
/// only asserted "ILastOwnerGuard is registered" would not have caught this class of bug as
/// reliably: it is the actual construction of the dependent services that fails.
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

        // This is the step SeederWorker.SyncOrganizationsAsync runs. It resolves IOrganizationService
        // (-> OrganizationService), whose constructor is where the missing ILastOwnerGuard
        // registration actually surfaces.
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
        typeof(IBootstrapAdminService),
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
