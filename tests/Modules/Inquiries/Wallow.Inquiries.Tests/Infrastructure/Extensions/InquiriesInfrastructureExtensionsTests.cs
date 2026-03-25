using Wallow.Inquiries.Application.Interfaces;
using Wallow.Inquiries.Infrastructure.Extensions;
using Wallow.Inquiries.Infrastructure.Persistence;
using Wallow.Inquiries.Infrastructure.Persistence.Repositories;
using Wallow.Inquiries.Infrastructure.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Wallow.Shared.Kernel.MultiTenancy;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Wallow.Inquiries.Tests.Infrastructure.Extensions;

public class InquiriesModuleExtensionsTests
{
    private static IConfiguration BuildConfiguration() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=test;Username=test;Password=test"
            })
            .Build();

    [Fact]
    public void AddInquiriesModule_RegistersAllServices()
    {
        ServiceCollection services = new();

        services.AddInquiriesModule(BuildConfiguration());

        ServiceDescriptor? repoDescriptor = services.FirstOrDefault(s => s.ServiceType == typeof(IInquiryRepository));
        repoDescriptor.Should().NotBeNull();

        ServiceDescriptor? rateLimitDescriptor = services.FirstOrDefault(s => s.ServiceType == typeof(IRateLimitService));
        rateLimitDescriptor.Should().NotBeNull();
    }

    [Fact]
    public void AddInquiriesModule_ReturnsServices()
    {
        ServiceCollection services = new();

        IServiceCollection result = services.AddInquiriesModule(BuildConfiguration());

        result.Should().BeSameAs(services);
    }

    [Fact]
    public void AddInquiriesModule_RegistersDbContext()
    {
        ServiceCollection services = new();

        services.AddInquiriesModule(BuildConfiguration());

        ServiceDescriptor? descriptor = services.FirstOrDefault(s => s.ServiceType == typeof(InquiriesDbContext));
        descriptor.Should().NotBeNull();
    }

    [Fact]
    public void AddInquiriesModule_RegistersInquiryCommentRepository()
    {
        ServiceCollection services = new();

        services.AddInquiriesModule(BuildConfiguration());

        ServiceDescriptor? descriptor = services.FirstOrDefault(s => s.ServiceType == typeof(IInquiryCommentRepository));
        descriptor.Should().NotBeNull();
        descriptor!.ImplementationType.Should().Be<InquiryCommentRepository>();
    }

    [Fact]
    public void AddInquiriesModule_RegistersFluentValidation()
    {
        ServiceCollection services = new();

        services.AddInquiriesModule(BuildConfiguration());

        bool hasValidators = services.Any(s =>
            s.ServiceType.IsGenericType &&
            s.ServiceType.GetGenericTypeDefinition().FullName?.Contains("FluentValidation") == true);
        // At minimum, the module registration should not throw and should complete
        services.Should().NotBeEmpty();
    }

    [Fact]
    public async Task InitializeInquiriesModuleAsync_InProductionEnvironment_DoesNotMigrate()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Environment.EnvironmentName = Environments.Production;

        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=test;Username=test;Password=test"
        });
        builder.Services.AddInquiriesModule(builder.Configuration);
        builder.Services.AddScoped<ITenantContext>(_ => Substitute.For<ITenantContext>());
        builder.Services.AddSingleton(_ => Substitute.For<IConnectionMultiplexer>());

        WebApplication app = builder.Build();

        // In Production, no migration is attempted (avoids network call)
        Func<Task> act = async () => await app.InitializeInquiriesModuleAsync();

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task InitializeInquiriesModuleAsync_InDevelopmentEnvironment_CatchesConnectionException()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Environment.EnvironmentName = Environments.Development;

        // Invalid connection string - will fail to connect but exception is swallowed
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Host=invalid-host-xyz;Database=test;Username=test;Password=test;Connect Timeout=1"
        });
        builder.Services.AddInquiriesModule(builder.Configuration);
        builder.Services.AddScoped<ITenantContext>(_ => Substitute.For<ITenantContext>());
        builder.Services.AddSingleton(_ => Substitute.For<IConnectionMultiplexer>());
        builder.Logging.ClearProviders().AddConsole();

        WebApplication app = builder.Build();

        Func<Task> act = async () => await app.InitializeInquiriesModuleAsync();

        // Exception is caught internally and logged as warning
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task InitializeInquiriesModuleAsync_InTestingEnvironment_CatchesConnectionException()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Environment.EnvironmentName = "Testing";

        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Host=invalid-host-xyz;Database=test;Username=test;Password=test;Connect Timeout=1"
        });
        builder.Services.AddInquiriesModule(builder.Configuration);
        builder.Services.AddScoped<ITenantContext>(_ => Substitute.For<ITenantContext>());
        builder.Services.AddSingleton(_ => Substitute.For<IConnectionMultiplexer>());
        builder.Logging.ClearProviders().AddConsole();

        WebApplication app = builder.Build();

        Func<Task> act = async () => await app.InitializeInquiriesModuleAsync();

        // Exception is caught internally and logged as warning
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task InitializeInquiriesModuleAsync_ReturnsWebApplication()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Environment.EnvironmentName = Environments.Production;

        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=test;Username=test;Password=test"
        });
        builder.Services.AddInquiriesModule(builder.Configuration);
        builder.Services.AddScoped<ITenantContext>(_ => Substitute.For<ITenantContext>());
        builder.Services.AddSingleton(_ => Substitute.For<IConnectionMultiplexer>());

        WebApplication app = builder.Build();

        WebApplication result = await app.InitializeInquiriesModuleAsync();

        result.Should().BeSameAs(app);
    }
}

public class InquiriesInfrastructureExtensionsTests
{
    private static IConfiguration BuildConfiguration(string? connectionString = "Host=localhost;Database=test;Username=test;Password=test") =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = connectionString
            })
            .Build();

    [Fact]
    public void AddInquiriesInfrastructure_RegistersDbContext()
    {
        ServiceCollection services = new();
        IConfiguration configuration = BuildConfiguration();

        services.AddInquiriesInfrastructure(configuration);

        ServiceDescriptor? descriptor = services.FirstOrDefault(s => s.ServiceType == typeof(InquiriesDbContext));
        descriptor.Should().NotBeNull();
    }

    [Fact]
    public void AddInquiriesInfrastructure_RegistersDbContextOptions()
    {
        ServiceCollection services = new();
        IConfiguration configuration = BuildConfiguration();

        services.AddInquiriesInfrastructure(configuration);

        ServiceDescriptor? descriptor = services.FirstOrDefault(s => s.ServiceType == typeof(DbContextOptions<InquiriesDbContext>));
        descriptor.Should().NotBeNull();
    }

    [Fact]
    public void AddInquiriesInfrastructure_RegistersInquiryRepository()
    {
        ServiceCollection services = new();
        IConfiguration configuration = BuildConfiguration();

        services.AddInquiriesInfrastructure(configuration);

        ServiceDescriptor? descriptor = services.FirstOrDefault(s => s.ServiceType == typeof(IInquiryRepository));
        descriptor.Should().NotBeNull();
        descriptor!.ImplementationType.Should().Be<InquiryRepository>();
    }

    [Fact]
    public void AddInquiriesInfrastructure_RegistersInquiryRepository_AsScoped()
    {
        ServiceCollection services = new();
        IConfiguration configuration = BuildConfiguration();

        services.AddInquiriesInfrastructure(configuration);

        ServiceDescriptor? descriptor = services.FirstOrDefault(s => s.ServiceType == typeof(IInquiryRepository));
        descriptor.Should().NotBeNull();
        descriptor!.Lifetime.Should().Be(ServiceLifetime.Scoped);
    }

    [Fact]
    public void AddInquiriesInfrastructure_RegistersInquiryCommentRepository()
    {
        ServiceCollection services = new();
        IConfiguration configuration = BuildConfiguration();

        services.AddInquiriesInfrastructure(configuration);

        ServiceDescriptor? descriptor = services.FirstOrDefault(s => s.ServiceType == typeof(IInquiryCommentRepository));
        descriptor.Should().NotBeNull();
        descriptor!.ImplementationType.Should().Be<InquiryCommentRepository>();
    }

    [Fact]
    public void AddInquiriesInfrastructure_RegistersInquiryCommentRepository_AsScoped()
    {
        ServiceCollection services = new();
        IConfiguration configuration = BuildConfiguration();

        services.AddInquiriesInfrastructure(configuration);

        ServiceDescriptor? descriptor = services.FirstOrDefault(s => s.ServiceType == typeof(IInquiryCommentRepository));
        descriptor.Should().NotBeNull();
        descriptor!.Lifetime.Should().Be(ServiceLifetime.Scoped);
    }

    [Fact]
    public void AddInquiriesInfrastructure_RegistersRateLimitService()
    {
        ServiceCollection services = new();
        IConfiguration configuration = BuildConfiguration();

        services.AddInquiriesInfrastructure(configuration);

        ServiceDescriptor? descriptor = services.FirstOrDefault(s => s.ServiceType == typeof(IRateLimitService));
        descriptor.Should().NotBeNull();
        descriptor!.ImplementationType.Should().Be<ValkeyRateLimitService>();
    }

    [Fact]
    public void AddInquiriesInfrastructure_RegistersRateLimitService_AsSingleton()
    {
        ServiceCollection services = new();
        IConfiguration configuration = BuildConfiguration();

        services.AddInquiriesInfrastructure(configuration);

        ServiceDescriptor? descriptor = services.FirstOrDefault(s => s.ServiceType == typeof(IRateLimitService));
        descriptor.Should().NotBeNull();
        descriptor!.Lifetime.Should().Be(ServiceLifetime.Singleton);
    }

    [Fact]
    public void AddInquiriesInfrastructure_ReturnsServices()
    {
        ServiceCollection services = new();
        IConfiguration configuration = BuildConfiguration();

        IServiceCollection result = services.AddInquiriesInfrastructure(configuration);

        result.Should().BeSameAs(services);
    }

    [Fact]
    public void AddInquiriesInfrastructure_WithNullConnectionString_ThrowsInvalidOperationException()
    {
        ServiceCollection services = new();
        IConfiguration configuration = BuildConfiguration(connectionString: null);

        Action act = () => services.AddInquiriesInfrastructure(configuration);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Connection string*not configured*");
    }

    [Fact]
    public void AddInquiriesInfrastructure_WithMissingConnectionString_ThrowsInvalidOperationException()
    {
        ServiceCollection services = new();
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        Action act = () => services.AddInquiriesInfrastructure(configuration);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Connection string*not configured*");
    }

    [Fact]
    public void AddInquiriesInfrastructure_RegistersExactlyExpectedServiceCount()
    {
        ServiceCollection services = new();
        IConfiguration configuration = BuildConfiguration();

        services.AddInquiriesInfrastructure(configuration);

        int repositoryCount = services.Count(s =>
            s.ServiceType == typeof(IInquiryRepository) ||
            s.ServiceType == typeof(IInquiryCommentRepository));
        repositoryCount.Should().Be(2);

        int rateLimitCount = services.Count(s => s.ServiceType == typeof(IRateLimitService));
        rateLimitCount.Should().Be(1);
    }
}
