using Foundry.Shared.Infrastructure.Core.Messaging;
using Wolverine;

namespace Foundry.Shared.Infrastructure.Tests.Messaging;

#pragma warning disable CA2000 // WolverineOptions does not expose a synchronous Dispose; safe to ignore in tests
public class WolverineErrorHandlingExtensionsTests
{
    [Fact]
    public void ConfigureStandardErrorHandling_DoesNotThrow()
    {
        WolverineOptions options = new();

        Action act = () => options.ConfigureStandardErrorHandling();

        act.Should().NotThrow();
    }

    [Fact]
    public void ConfigureStandardErrorHandling_RegistersFailurePolicies()
    {
        WolverineOptions options = new();

        options.ConfigureStandardErrorHandling();

        options.Policies.Failures.Should().NotBeNull();
    }

    [Fact]
    public void ConfigureStandardErrorHandling_CanBeCalledMultipleTimes()
    {
        WolverineOptions options = new();

        Action act = () =>
        {
            options.ConfigureStandardErrorHandling();
            options.ConfigureStandardErrorHandling();
        };

        act.Should().NotThrow();
    }

    [Fact]
    public void ConfigureMessageLogging_DoesNotThrow()
    {
        WolverineOptions options = new();

        Action act = () => options.ConfigureMessageLogging();

        act.Should().NotThrow();
    }

    [Fact]
    public void ConfigureMessageLogging_CanBeCalledMultipleTimes()
    {
        WolverineOptions options = new();

        Action act = () =>
        {
            options.ConfigureMessageLogging();
            options.ConfigureMessageLogging();
        };

        act.Should().NotThrow();
    }

    [Fact]
    public void ConfigureStandardErrorHandling_AndConfigureMessageLogging_CanBeCombined()
    {
        WolverineOptions options = new();

        Action act = () =>
        {
            options.ConfigureStandardErrorHandling();
            options.ConfigureMessageLogging();
        };

        act.Should().NotThrow();
    }
}
#pragma warning restore CA2000
