using Microsoft.Extensions.DependencyInjection;
using Wallow.Shared.Kernel.Extensions;
using Wallow.Shared.Kernel.MultiTenancy;

namespace Wallow.Shared.Kernel.Tests.Extensions;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddSharedKernel_RegistersTimeProvider()
    {
        ServiceCollection services = new();

        services.AddSharedKernel();

        ServiceProvider provider = services.BuildServiceProvider();
        TimeProvider timeProvider = provider.GetRequiredService<TimeProvider>();
        timeProvider.Should().Be(TimeProvider.System);
    }

    [Fact]
    public void AddSharedKernel_RegistersTenantContext_AsScoped()
    {
        ServiceCollection services = new();

        services.AddSharedKernel();

        ServiceDescriptor? descriptor = services.FirstOrDefault(
            d => d.ServiceType == typeof(TenantContext));
        descriptor.Should().NotBeNull();
        descriptor.Lifetime.Should().Be(ServiceLifetime.Scoped);
    }

    [Fact]
    public void AddSharedKernel_RegistersITenantContext_ResolvesToTenantContext()
    {
        ServiceCollection services = new();

        services.AddSharedKernel();

        ServiceProvider provider = services.BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();
        ITenantContext tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        TenantContext concreteContext = scope.ServiceProvider.GetRequiredService<TenantContext>();

        tenantContext.Should().BeSameAs(concreteContext);
    }

    [Fact]
    public void AddSharedKernel_RegistersTenantSaveChangesInterceptor_AsSingleton()
    {
        ServiceCollection services = new();

        services.AddSharedKernel();

        ServiceDescriptor? descriptor = services.FirstOrDefault(
            d => d.ServiceType == typeof(TenantSaveChangesInterceptor));
        descriptor.Should().NotBeNull();
        descriptor.Lifetime.Should().Be(ServiceLifetime.Singleton);
    }

    [Fact]
    public void AddSharedKernel_RegistersITenantContextFactory_AsScoped()
    {
        ServiceCollection services = new();

        services.AddSharedKernel();

        ServiceDescriptor? descriptor = services.FirstOrDefault(
            d => d.ServiceType == typeof(ITenantContextFactory));
        descriptor.Should().NotBeNull();
        descriptor.Lifetime.Should().Be(ServiceLifetime.Scoped);
    }

    [Fact]
    public void AddSharedKernel_CanResolveAllRegisteredServices()
    {
        ServiceCollection services = new();

        services.AddSharedKernel();

        ServiceProvider provider = services.BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();
        IServiceProvider sp = scope.ServiceProvider;

        sp.GetRequiredService<TimeProvider>().Should().NotBeNull();
        sp.GetRequiredService<TenantContext>().Should().NotBeNull();
        sp.GetRequiredService<ITenantContext>().Should().NotBeNull();
        sp.GetRequiredService<TenantSaveChangesInterceptor>().Should().NotBeNull();
        sp.GetRequiredService<ITenantContextFactory>().Should().NotBeNull();
    }

    [Fact]
    public void AddSharedKernel_ReturnsServiceCollection_ForChaining()
    {
        ServiceCollection services = new();

        IServiceCollection result = services.AddSharedKernel();

        result.Should().BeSameAs(services);
    }
}
