using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wallow.ServiceDefaults;

namespace Wallow.SeederService.Tests;

/// <summary>
/// Wallow-2y1t: a seed step that threw was logged Critical and then swallowed by the host, which
/// exits the process 0. docker-compose.test.yml gates wallow-api and wallow-web on the seeder's
/// <c>service_completed_successfully</c>, so a failed seed silently started the stack against a
/// database with zero OIDC clients. These tests pin the failure onto <see cref="WorkerRunOutcome"/>,
/// which Program.cs turns into a non-zero exit.
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
        // The happy path must not regress: a successful seed still has to satisfy Compose's
        // service_completed_successfully edge.
        _outcome.Failed.Should().BeFalse();
        _outcome.ExitCode.Should().Be(0);
    }

    /// <summary>
    /// Drives the real <c>ExecuteAsync</c>. <c>StartAsync</c> returns as soon as the worker yields,
    /// so the faulted task must be awaited through <c>ExecuteTask</c>; awaiting only
    /// <c>StartAsync</c> would miss a failure that happens after the first await.
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
        // An empty provider: the first step's GetRequiredService<RoleManager<WallowRole>>() throws,
        // which is exactly how the Wallow-smvc DI gap surfaced in the wild.
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
