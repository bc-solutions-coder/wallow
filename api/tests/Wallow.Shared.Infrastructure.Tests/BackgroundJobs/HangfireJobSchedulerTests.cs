using Microsoft.Extensions.DependencyInjection;
using Wallow.Shared.Infrastructure.BackgroundJobs;
using Wallow.Shared.Kernel.BackgroundJobs;

namespace Wallow.Shared.Infrastructure.Tests.BackgroundJobs;

public class HangfireJobSchedulerTests
{
    [Fact]
    public void HangfireJobScheduler_Implements_IJobScheduler()
    {
        HangfireJobScheduler scheduler = new();

        IJobScheduler jobScheduler = scheduler;

        jobScheduler.Should().NotBeNull();
        jobScheduler.Should().BeOfType<HangfireJobScheduler>();
    }

    [Fact]
    public void AddWallowBackgroundJobs_Registers_IJobScheduler_As_HangfireJobScheduler()
    {
        ServiceCollection services = new();

        services.AddWallowBackgroundJobs();

        ServiceProvider provider = services.BuildServiceProvider();
        IJobScheduler resolved = provider.GetRequiredService<IJobScheduler>();

        resolved.Should().NotBeNull();
        resolved.Should().BeOfType<HangfireJobScheduler>();
    }

    [Fact]
    public void AddWallowBackgroundJobs_Registers_As_Singleton()
    {
        ServiceCollection services = new();

        services.AddWallowBackgroundJobs();

        ServiceDescriptor descriptor = services.Should().ContainSingle(
            sd => sd.ServiceType == typeof(IJobScheduler)).Subject;

        descriptor.Lifetime.Should().Be(ServiceLifetime.Singleton);
        descriptor.ImplementationType.Should().Be<HangfireJobScheduler>();
    }

    [Fact]
    public void AddWallowBackgroundJobs_Returns_Same_Instance_Across_Scopes()
    {
        ServiceCollection services = new();
        services.AddWallowBackgroundJobs();
        ServiceProvider provider = services.BuildServiceProvider();

        IJobScheduler first = provider.GetRequiredService<IJobScheduler>();
        IJobScheduler second = provider.GetRequiredService<IJobScheduler>();

        first.Should().BeSameAs(second);
    }

    [Fact]
    public void Enqueue_WithoutJobStorage_ThrowsInvalidOperationException()
    {
        HangfireJobScheduler scheduler = new();

        Action act = () => scheduler.Enqueue(() => Task.CompletedTask);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Enqueue_Generic_WithoutJobStorage_ThrowsInvalidOperationException()
    {
        HangfireJobScheduler scheduler = new();

        Action act = () => scheduler.Enqueue<IJobScheduler>(x => Task.CompletedTask);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void AddRecurring_WithoutJobStorage_Throws()
    {
        HangfireJobScheduler scheduler = new();

        Action act = () => scheduler.AddRecurring("test-job", "0 * * * *", () => Task.CompletedTask);

        // Hangfire validates the expression before checking storage, so the specific
        // exception type depends on the expression. What matters is that the call
        // delegates to Hangfire and does not silently succeed.
        act.Should().Throw<Exception>();
    }

    [Fact]
    public void RemoveRecurring_WithoutJobStorage_ThrowsInvalidOperationException()
    {
        HangfireJobScheduler scheduler = new();

        Action act = () => scheduler.RemoveRecurring("test-job");

        act.Should().Throw<InvalidOperationException>();
    }
}
