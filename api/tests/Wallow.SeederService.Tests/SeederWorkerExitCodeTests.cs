using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wallow.ServiceDefaults;
using Wallow.Tests.Common;

namespace Wallow.SeederService.Tests;

/// <summary>
/// Checks failed-run status, critical logging and shutdown after a seed-step exception.
/// </summary>
public class SeederWorkerExitCodeTests
{
    private readonly IServiceScopeFactory _scopeFactory = Substitute.For<IServiceScopeFactory>();
    private readonly IHostApplicationLifetime _lifetime = Substitute.For<IHostApplicationLifetime>();
    private readonly RecordingLogger<SeederWorker> _logger = new();
    private readonly WorkerRunOutcome _outcome = new();

    [Fact]
    public async Task ExecuteAsync_WhenASeedStepThrows_MarksTheRunFailed()
    {
        using SeederWorker worker = CreateWorkerWithEmptyScope();

        await RunToCompletionAsync(worker).Should().ThrowAsync<InvalidOperationException>();

        _outcome.Failed.Should().BeTrue(
            "a thrown seed step must reach Program.cs as a non-zero exit code, or Compose's "
            + "service_completed_successfully gate lets dependents start against a half-seeded database");
        _outcome.ExitCode.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_WhenASeedStepThrows_LogsCritical()
    {
        using SeederWorker worker = CreateWorkerWithEmptyScope();

        await RunToCompletionAsync(worker).Should().ThrowAsync<InvalidOperationException>();

        _logger.Entries.Should().Contain(e => e.Level == LogLevel.Critical);
    }

    [Fact]
    public async Task ExecuteAsync_WhenASeedStepThrows_StillStopsTheApplication()
    {
        using SeederWorker worker = CreateWorkerWithEmptyScope();

        await RunToCompletionAsync(worker).Should().ThrowAsync<InvalidOperationException>();

        _lifetime.Received(1).StopApplication();
    }

    [Fact]
    public void WorkerRunOutcome_BeforeAnyFailure_ExitsZero()
    {

        _outcome.Failed.Should().BeFalse();
        _outcome.ExitCode.Should().Be(0);
    }

    /// <summary>
    /// Awaits the worker execution task so failures after startup are observed.
    /// </summary>
    private static Func<Task> RunToCompletionAsync(SeederWorker worker) => async () =>
    {
        await worker.StartAsync(CancellationToken.None);

        if (worker.ExecuteTask is not null)
        {
            await worker.ExecuteTask;
        }
    };

    private SeederWorker CreateWorkerWithEmptyScope()
    {
        // Missing services force the worker to fail during seeding.
        ServiceCollection services = new();
        ServiceProvider emptyProvider = services.BuildServiceProvider();

        IServiceScope scope = Substitute.For<IServiceScope>();
        scope.ServiceProvider.Returns(emptyProvider);

        _scopeFactory.CreateScope().Returns(scope);

        return new SeederWorker(
            _scopeFactory,
            Options.Create(new SeedOptions()),
            _lifetime,
            _outcome,
            _logger);
    }
}
