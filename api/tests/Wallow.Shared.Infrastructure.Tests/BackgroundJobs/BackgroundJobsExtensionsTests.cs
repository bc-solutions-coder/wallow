using Microsoft.Extensions.DependencyInjection;
using Wallow.Shared.Infrastructure.BackgroundJobs;
using Wallow.Shared.Kernel.BackgroundJobs;

namespace Wallow.Shared.Infrastructure.Tests.BackgroundJobs;

public class BackgroundJobsExtensionsTests
{
    [Fact]
    public void AddWallowBackgroundJobs_RegistersIJobScheduler()
    {
        ServiceCollection services = new();

        services.AddWallowBackgroundJobs();

        services.Should().ContainSingle(sd => sd.ServiceType == typeof(IJobScheduler));
    }

    [Fact]
    public void AddWallowBackgroundJobs_RegistersHangfireJobSchedulerAsImplementation()
    {
        ServiceCollection services = new();

        services.AddWallowBackgroundJobs();

        ServiceDescriptor descriptor = services.Single(sd => sd.ServiceType == typeof(IJobScheduler));
        descriptor.ImplementationType.Should().Be<HangfireJobScheduler>();
    }

    [Fact]
    public void AddWallowBackgroundJobs_RegistersAsSingleton()
    {
        ServiceCollection services = new();

        services.AddWallowBackgroundJobs();

        ServiceDescriptor descriptor = services.Single(sd => sd.ServiceType == typeof(IJobScheduler));
        descriptor.Lifetime.Should().Be(ServiceLifetime.Singleton);
    }

    [Fact]
    public void AddWallowBackgroundJobs_ReturnsServiceCollection()
    {
        ServiceCollection services = new();

        IServiceCollection result = services.AddWallowBackgroundJobs();

        result.Should().BeSameAs(services);
    }

    [Fact]
    public void AddWallowBackgroundJobs_ResolvesCorrectly()
    {
        ServiceCollection services = new();
        services.AddWallowBackgroundJobs();
        ServiceProvider provider = services.BuildServiceProvider();

        IJobScheduler resolved = provider.GetRequiredService<IJobScheduler>();

        resolved.Should().NotBeNull();
        resolved.Should().BeOfType<HangfireJobScheduler>();
    }
}
