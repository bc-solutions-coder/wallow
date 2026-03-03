using Foundry.Shared.Infrastructure.Messaging;
using Wolverine;

namespace Foundry.Shared.Kernel.Tests.Messaging;

#pragma warning disable CA2000 // WolverineOptions does not implement IDisposable

public class WolverineErrorHandlingExtensionsTests
{
    [Fact]
    public void ConfigureStandardErrorHandling_DoesNotThrow()
    {
        WolverineOptions opts = new();

        Action act = () => opts.ConfigureStandardErrorHandling();

        act.Should().NotThrow();
    }

    [Fact]
    public void ConfigureStandardErrorHandling_ConfiguresFailurePolicies()
    {
        WolverineOptions opts = new();

        opts.ConfigureStandardErrorHandling();

        opts.Policies.Failures.Should().NotBeNull();
    }

    [Fact]
    public void ConfigureMessageLogging_DoesNotThrow()
    {
        WolverineOptions opts = new();

        Action act = () => opts.ConfigureMessageLogging();

        act.Should().NotThrow();
    }

    [Fact]
    public void ConfigureStandardErrorHandling_CanBeCalledMultipleTimes_WithoutError()
    {
        WolverineOptions opts = new();

        Action act = () =>
        {
            opts.ConfigureStandardErrorHandling();
            opts.ConfigureStandardErrorHandling();
        };

        act.Should().NotThrow();
    }

    [Fact]
    public void ConfigureMessageLogging_CanBeCalledMultipleTimes_WithoutError()
    {
        WolverineOptions opts = new();

        Action act = () =>
        {
            opts.ConfigureMessageLogging();
            opts.ConfigureMessageLogging();
        };

        act.Should().NotThrow();
    }
}
