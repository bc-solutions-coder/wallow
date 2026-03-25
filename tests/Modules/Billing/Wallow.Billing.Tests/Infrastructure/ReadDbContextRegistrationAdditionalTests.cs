using Wallow.ApiKeys.Infrastructure.Extensions;
using Wallow.ApiKeys.Infrastructure.Persistence;
using Wallow.Branding.Infrastructure.Extensions;
using Wallow.Branding.Infrastructure.Persistence;
using Wallow.Inquiries.Infrastructure.Extensions;
using Wallow.Inquiries.Infrastructure.Persistence;
using Wallow.Shared.Infrastructure.Core.Auditing;
using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.MultiTenancy;
using Wallow.Shared.Kernel.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace Wallow.Billing.Tests.Infrastructure;

public sealed class ReadDbContextRegistrationAdditionalTests
{
    private const string DefaultConnection = "Host=localhost;Database=test_default;";

    private static IConfiguration BuildConfiguration()
    {
        Dictionary<string, string?> config = new()
        {
            ["ConnectionStrings:DefaultConnection"] = DefaultConnection
        };
        return new ConfigurationBuilder()
            .AddInMemoryCollection(config)
            .Build();
    }

    private static void RegisterCommonDependencies(IServiceCollection services)
    {
        services.AddSingleton<TenantSaveChangesInterceptor>(_ =>
        {
            ITenantContext tenantContext = Substitute.For<ITenantContext>();
            tenantContext.TenantId.Returns(TenantId.New());
            return new TenantSaveChangesInterceptor(tenantContext);
        });
        services.AddSingleton<ITenantContext>(_ =>
        {
            ITenantContext tenantContext = Substitute.For<ITenantContext>();
            tenantContext.TenantId.Returns(TenantId.New());
            return tenantContext;
        });
    }

    [Fact]
    public void AddBrandingInfrastructure_RegistersReadDbContextForBrandingDbContext()
    {
        ServiceCollection services = new();
        IConfiguration configuration = BuildConfiguration();
        RegisterCommonDependencies(services);

        services.AddBrandingInfrastructure(configuration);

        ServiceProvider provider = services.BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();
        IReadDbContext<BrandingDbContext>? readDbContext = scope.ServiceProvider
            .GetService<IReadDbContext<BrandingDbContext>>();

        readDbContext.Should().NotBeNull();
    }

    [Fact]
    public void AddInquiriesInfrastructure_RegistersReadDbContextForInquiriesDbContext()
    {
        ServiceCollection services = new();
        IConfiguration configuration = BuildConfiguration();
        RegisterCommonDependencies(services);

        services.AddInquiriesInfrastructure(configuration);

        ServiceProvider provider = services.BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();
        IReadDbContext<InquiriesDbContext>? readDbContext = scope.ServiceProvider
            .GetService<IReadDbContext<InquiriesDbContext>>();

        readDbContext.Should().NotBeNull();
    }

    [Fact]
    public void AddApiKeysInfrastructure_RegistersReadDbContextForApiKeysDbContext()
    {
        ServiceCollection services = new();
        IConfiguration configuration = BuildConfiguration();
        RegisterCommonDependencies(services);
        services.AddSingleton(Substitute.For<IConnectionMultiplexer>());

        services.AddApiKeysInfrastructure(configuration);

        ServiceProvider provider = services.BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();
        IReadDbContext<ApiKeysDbContext>? readDbContext = scope.ServiceProvider
            .GetService<IReadDbContext<ApiKeysDbContext>>();

        readDbContext.Should().NotBeNull();
    }

    [Fact]
    public void AddWallowAuditing_RegistersReadDbContextForAuditDbContext()
    {
        ServiceCollection services = new();
        IConfiguration configuration = BuildConfiguration();

        services.AddWallowAuditing(configuration);

        ServiceProvider provider = services.BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();
        IReadDbContext<AuditDbContext>? readDbContext = scope.ServiceProvider
            .GetService<IReadDbContext<AuditDbContext>>();

        readDbContext.Should().NotBeNull();
    }
}
