using Foundry.Inquiries.Application.Interfaces;
using Foundry.Inquiries.Infrastructure.Extensions;
using Foundry.Inquiries.Infrastructure.Persistence;
using Foundry.Inquiries.Infrastructure.Persistence.Repositories;
using Foundry.Inquiries.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Foundry.Inquiries.Tests.Infrastructure.Extensions;

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
}

public class InquiriesInfrastructureExtensionsTests
{
    [Fact]
    public void AddInquiriesInfrastructure_RegistersDbContext()
    {
        ServiceCollection services = new();
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=test;Username=test;Password=test"
            })
            .Build();

        services.AddInquiriesInfrastructure(configuration);

        ServiceDescriptor? descriptor = services.FirstOrDefault(s => s.ServiceType == typeof(InquiriesDbContext));
        descriptor.Should().NotBeNull();
    }

    [Fact]
    public void AddInquiriesInfrastructure_RegistersInquiryRepository()
    {
        ServiceCollection services = new();
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=test;Username=test;Password=test"
            })
            .Build();

        services.AddInquiriesInfrastructure(configuration);

        ServiceDescriptor? descriptor = services.FirstOrDefault(s => s.ServiceType == typeof(IInquiryRepository));
        descriptor.Should().NotBeNull();
        descriptor!.ImplementationType.Should().Be<InquiryRepository>();
    }

    [Fact]
    public void AddInquiriesInfrastructure_RegistersRateLimitService()
    {
        ServiceCollection services = new();
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=test;Username=test;Password=test"
            })
            .Build();

        services.AddInquiriesInfrastructure(configuration);

        ServiceDescriptor? descriptor = services.FirstOrDefault(s => s.ServiceType == typeof(IRateLimitService));
        descriptor.Should().NotBeNull();
        descriptor!.ImplementationType.Should().Be<ValkeyRateLimitService>();
    }

    [Fact]
    public void AddInquiriesInfrastructure_ReturnsServices()
    {
        ServiceCollection services = new();
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=test;Username=test;Password=test"
            })
            .Build();

        IServiceCollection result = services.AddInquiriesInfrastructure(configuration);

        result.Should().BeSameAs(services);
    }
}
