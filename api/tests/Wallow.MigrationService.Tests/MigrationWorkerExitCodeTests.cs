using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Wallow.ServiceDefaults;
using Wallow.Tests.Common;

namespace Wallow.MigrationService.Tests;

/// <summary>
/// Wallow-2y1t: MigrationWorker.ExecuteAsync had no try/catch at all, so a failed migration was
/// neither logged Critical by our own code nor reflected in the process exit code — the host
/// swallowed it and the container exited 0. docker-compose.test.yml gates wallow-seeder on
/// wallow-migrations' service_completed_successfully, so an unmigrated database cascaded silently.
/// </summary>
public class MigrationWorkerExitCodeTests
{
    private readonly IHostApplicationLifetime _lifetime = Substitute.For<IHostApplicationLifetime>();
    private readonly RecordingLogger<MigrationWorker> _logger = new();
    private readonly WorkerRunOutcome _outcome = new();

    [Fact]
    public async Task ExecuteAsync_WhenACoreMigrationThrows_MarksTheRunFailed()
    {
        using MigrationWorker worker = CreateWorker(ThrowingRunner());

        await RunToCompletionAsync(worker).Should().ThrowAsync<InvalidOperationException>();

        _outcome.Failed.Should().BeTrue();
        _outcome.ExitCode.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_WhenACoreMigrationThrows_LogsCritical()
    {
        using MigrationWorker worker = CreateWorker(ThrowingRunner());

        await RunToCompletionAsync(worker).Should().ThrowAsync<InvalidOperationException>();

        _logger.Entries.Should().Contain(e => e.Level == LogLevel.Critical);
    }

    [Fact]
    public async Task ExecuteAsync_WhenACoreMigrationThrows_StopsTheApplication()
    {
        using MigrationWorker worker = CreateWorker(ThrowingRunner());

        await RunToCompletionAsync(worker).Should().ThrowAsync<InvalidOperationException>();

        // Before this change the failure path never called StopApplication at all; only the host's
        // own StopHost default stopped the process.
        _lifetime.Received(1).StopApplication();
    }

    [Fact]
    public async Task ExecuteAsync_WhenEveryMigrationSucceeds_ExitsZero()
    {
        using MigrationWorker worker = CreateWorker(SucceedingRunner());

        await RunToCompletionAsync(worker)();

        _outcome.Failed.Should().BeFalse();
        _outcome.ExitCode.Should().Be(0);
        _lifetime.Received(1).StopApplication();
    }

    private static Func<Task> RunToCompletionAsync(MigrationWorker worker) => async () =>
    {
        await worker.StartAsync(CancellationToken.None);

        if (worker.ExecuteTask is not null)
        {
            await worker.ExecuteTask;
        }
    };

    private static IMigrationRunner ThrowingRunner()
    {
        IMigrationRunner runner = Substitute.For<IMigrationRunner>();
        runner.ContextName.Returns("FailingDbContext");
        runner.MigrateAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("migration failed")));
        return runner;
    }

    private static IMigrationRunner SucceedingRunner()
    {
        IMigrationRunner runner = Substitute.For<IMigrationRunner>();
        runner.ContextName.Returns("HealthyDbContext");
        runner.MigrateAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        return runner;
    }

    private MigrationWorker CreateWorker(IMigrationRunner coreRunner) =>
        new(
            new CoreMigrationRunners([coreRunner]),
            new FeatureMigrationRunners([]),
            _lifetime,
            _outcome,
            _logger);
}
