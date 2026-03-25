using Wallow.Announcements.Infrastructure.Extensions;
using Wallow.Announcements.Infrastructure.Persistence;
using Wallow.Billing.Infrastructure.Persistence;
using Wallow.Identity.Infrastructure.Extensions;
using Wallow.Identity.Infrastructure.Persistence;
using Wallow.Notifications.Infrastructure.Extensions;
using Wallow.Notifications.Infrastructure.Persistence;
using Wallow.Shared.Infrastructure.Core.Extensions;
using Wallow.Shared.Kernel.Persistence;
using Wallow.Storage.Infrastructure.Extensions;
using Wallow.Storage.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using StackExchange.Redis;

namespace Wallow.Billing.Tests.Infrastructure;

public sealed class ReadDbContextRegistrationTests
{
    private const string DefaultConnection = "Host=localhost;Database=test_default;";
    private const string ReplicaConnection = "Host=replica-host;Database=test_replica;";

    [Fact]
    public void AddReadDbContext_WithNoReadReplicaConnection_ResolvesUsingDefaultConnection()
    {
        ServiceCollection services = new();
        Dictionary<string, string?> config = new()
        {
            ["ConnectionStrings:DefaultConnection"] = DefaultConnection
        };
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(config)
            .Build();

        services.AddReadDbContext<BillingDbContext>(configuration);

        ServiceProvider provider = services.BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();
        IReadDbContext<BillingDbContext> readDbContext = scope.ServiceProvider
            .GetRequiredService<IReadDbContext<BillingDbContext>>();

        readDbContext.Should().NotBeNull();
    }

    [Fact]
    public void AddReadDbContext_WithEmptyReadReplicaConnection_ResolvesUsingFallback()
    {
        ServiceCollection services = new();
        Dictionary<string, string?> config = new()
        {
            ["ConnectionStrings:DefaultConnection"] = DefaultConnection,
            ["ConnectionStrings:ReadReplicaConnection"] = ""
        };
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(config)
            .Build();

        services.AddReadDbContext<BillingDbContext>(configuration);

        ServiceProvider provider = services.BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();
        IReadDbContext<BillingDbContext> readDbContext = scope.ServiceProvider
            .GetRequiredService<IReadDbContext<BillingDbContext>>();

        readDbContext.Should().NotBeNull();
    }

    [Fact]
    public void AddReadDbContext_WithDistinctReadReplicaConnection_UsesReplicaConnectionString()
    {
        ServiceCollection services = new();
        Dictionary<string, string?> config = new()
        {
            ["ConnectionStrings:DefaultConnection"] = DefaultConnection,
            ["ConnectionStrings:ReadReplicaConnection"] = ReplicaConnection
        };
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(config)
            .Build();

        services.AddReadDbContext<BillingDbContext>(configuration);

        ServiceProvider provider = services.BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();
        IReadDbContext<BillingDbContext> readDbContext = scope.ServiceProvider
            .GetRequiredService<IReadDbContext<BillingDbContext>>();

        string? connectionString = readDbContext.Context.Database.GetConnectionString();
        connectionString.Should().Be(ReplicaConnection);
    }

    [Fact]
    public void AddReadDbContext_ResolvedContext_SaveChangesThrowsInvalidOperationException()
    {
        ServiceCollection services = new();
        Dictionary<string, string?> config = new()
        {
            ["ConnectionStrings:DefaultConnection"] = DefaultConnection
        };
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(config)
            .Build();

        services.AddReadDbContext<BillingDbContext>(configuration);

        ServiceProvider provider = services.BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();
        IReadDbContext<BillingDbContext> readDbContext = scope.ServiceProvider
            .GetRequiredService<IReadDbContext<BillingDbContext>>();

        Action act = () => readDbContext.Context.SaveChanges();

        act.Should().Throw<InvalidOperationException>();
    }

    private static IConfiguration BuildModuleConfiguration() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = DefaultConnection
            })
            .Build();

    [Fact]
    public void AddIdentityModule_RegistersReadDbContextForIdentity_AsScopedService()
    {
        ServiceCollection services = new();
        IConfiguration configuration = BuildModuleConfiguration();
        IHostEnvironment environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(Environments.Development);
        services.AddSingleton(Substitute.For<IConnectionMultiplexer>());

        services.AddIdentityModule(configuration, environment);

        ServiceDescriptor? descriptor = services.FirstOrDefault(
            s => s.ServiceType == typeof(IReadDbContext<IdentityDbContext>));
        descriptor.Should().NotBeNull();
        descriptor!.Lifetime.Should().Be(ServiceLifetime.Scoped);
    }

    [Fact]
    public void AddStorageModule_RegistersReadDbContextForStorage_AsScopedService()
    {
        ServiceCollection services = new();
        IConfiguration configuration = BuildModuleConfiguration();

        services.AddStorageModule(configuration);

        ServiceDescriptor? descriptor = services.FirstOrDefault(
            s => s.ServiceType == typeof(IReadDbContext<StorageDbContext>));
        descriptor.Should().NotBeNull();
        descriptor!.Lifetime.Should().Be(ServiceLifetime.Scoped);
    }

    [Fact]
    public void AddNotificationsModule_RegistersReadDbContextForNotifications_AsScopedService()
    {
        ServiceCollection services = new();
        IConfiguration configuration = BuildModuleConfiguration();

        services.AddNotificationsModule(configuration);

        ServiceDescriptor? descriptor = services.FirstOrDefault(
            s => s.ServiceType == typeof(IReadDbContext<NotificationsDbContext>));
        descriptor.Should().NotBeNull();
        descriptor!.Lifetime.Should().Be(ServiceLifetime.Scoped);
    }

    [Fact]
    public void AddAnnouncementsModule_RegistersReadDbContextForAnnouncements_AsScopedService()
    {
        ServiceCollection services = new();
        IConfiguration configuration = BuildModuleConfiguration();

        services.AddAnnouncementsModule(configuration);

        ServiceDescriptor? descriptor = services.FirstOrDefault(
            s => s.ServiceType == typeof(IReadDbContext<AnnouncementsDbContext>));
        descriptor.Should().NotBeNull();
        descriptor!.Lifetime.Should().Be(ServiceLifetime.Scoped);
    }
}
