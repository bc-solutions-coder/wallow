using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Wallow.MigrationService;

namespace Wallow.Architecture.Tests;

public sealed class MigrationServiceTests : IDisposable
{
    private readonly IHostApplicationLifetime _lifetime;
    private readonly MigrationWorker _worker;
    private readonly List<string> _migrationOrder;

    private readonly IMigrationRunner _identityRunner;
    private readonly IMigrationRunner _auditRunner;
    private readonly IMigrationRunner _authAuditRunner;
    private readonly IMigrationRunner _brandingRunner;
    private readonly IMigrationRunner _notificationsRunner;
    private readonly IMigrationRunner _announcementsRunner;
    private readonly IMigrationRunner _storageRunner;
    private readonly IMigrationRunner _apiKeysRunner;
    private readonly IMigrationRunner _inquiriesRunner;

    public MigrationServiceTests()
    {
        _lifetime = Substitute.For<IHostApplicationLifetime>();
        _migrationOrder = [];

        _identityRunner = CreateMockRunner("Identity");
        _auditRunner = CreateMockRunner("Audit");
        _authAuditRunner = CreateMockRunner("AuthAudit");
        _brandingRunner = CreateMockRunner("Branding");
        _notificationsRunner = CreateMockRunner("Notifications");
        _announcementsRunner = CreateMockRunner("Announcements");
        _storageRunner = CreateMockRunner("Storage");
        _apiKeysRunner = CreateMockRunner("ApiKeys");
        _inquiriesRunner = CreateMockRunner("Inquiries");

        CoreMigrationRunners coreRunners = new([_identityRunner, _auditRunner, _authAuditRunner]);
        FeatureMigrationRunners featureRunners = new([_brandingRunner, _notificationsRunner, _announcementsRunner, _storageRunner, _apiKeysRunner, _inquiriesRunner]);

        _worker = new MigrationWorker(
            coreRunners,
            featureRunners,
            _lifetime,
            NullLogger<MigrationWorker>.Instance);
    }

    [Fact]
    public async Task ExecuteAsync_CallsStopApplication_AfterAllMigrationsComplete()
    {
        // Act
        await ExecuteWorkerAsync();

        // Assert
        _lifetime.Received(1).StopApplication();
    }

    [Fact]
    public async Task ExecuteAsync_MigratesIdentityAndAuditContexts_BeforeFeatureModules()
    {
        // Act
        await ExecuteWorkerAsync();

        // Assert: Identity, Audit, and AuthAudit should be migrated before any feature module
        int identityIndex = _migrationOrder.IndexOf("Identity");
        int auditIndex = _migrationOrder.IndexOf("Audit");
        int authAuditIndex = _migrationOrder.IndexOf("AuthAudit");

        identityIndex.Should().BeGreaterThanOrEqualTo(0, "Identity migration should have been called");
        auditIndex.Should().BeGreaterThanOrEqualTo(0, "Audit migration should have been called");
        authAuditIndex.Should().BeGreaterThanOrEqualTo(0, "AuthAudit migration should have been called");

        // All feature modules should come after the identity/audit group
        int firstFeatureIndex = GetFirstFeatureModuleIndex();
        firstFeatureIndex.Should().BeGreaterThanOrEqualTo(0, "at least one feature module migration should have been called");

        int lastCoreIndex = Math.Max(identityIndex, Math.Max(auditIndex, authAuditIndex));
        firstFeatureIndex.Should().BeGreaterThan(lastCoreIndex,
            "feature modules should be migrated after identity and audit contexts");
    }

    [Fact]
    public async Task ExecuteAsync_MigratesAllNineDbContexts()
    {
        // Act
        await ExecuteWorkerAsync();

        // Assert
        _migrationOrder.Should().Contain("Identity");
        _migrationOrder.Should().Contain("Audit");
        _migrationOrder.Should().Contain("AuthAudit");
        _migrationOrder.Should().Contain("Branding");
        _migrationOrder.Should().Contain("Notifications");
        _migrationOrder.Should().Contain("Announcements");
        _migrationOrder.Should().Contain("Storage");
        _migrationOrder.Should().Contain("ApiKeys");
        _migrationOrder.Should().Contain("Inquiries");
        _migrationOrder.Should().HaveCount(9);
    }

    [Fact]
    public async Task ExecuteAsync_WhenMigrationThrows_PropagatesException()
    {
        // Arrange: Make identity migration throw
        InvalidOperationException expectedException = new("Database connection failed");
        _identityRunner.MigrateAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromException(expectedException));

        // Act
        Func<Task> act = () => ExecuteWorkerAsync();

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Database connection failed");
    }

    [Fact]
    public async Task ExecuteAsync_WhenFeatureModuleMigrationThrows_PropagatesException()
    {
        // Arrange: Make a feature module migration throw
        InvalidOperationException expectedException = new("Branding migration failed");
        _brandingRunner.MigrateAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromException(expectedException));

        // Act
        Func<Task> act = () => ExecuteWorkerAsync();

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Branding migration failed");
    }

    [Fact]
    public async Task ExecuteAsync_CallsStopApplication_WhenAllMigrationsSucceed()
    {
        // Act
        await ExecuteWorkerAsync();

        // Assert: StopApplication must be called even when everything succeeds cleanly
        _lifetime.Received(1).StopApplication();
    }

    [Fact]
    public async Task ExecuteAsync_CallsMigrateOnAllRunners()
    {
        // Act
        await ExecuteWorkerAsync();

        // Assert
        await _identityRunner.Received(1).MigrateAsync(Arg.Any<CancellationToken>());
        await _auditRunner.Received(1).MigrateAsync(Arg.Any<CancellationToken>());
        await _authAuditRunner.Received(1).MigrateAsync(Arg.Any<CancellationToken>());
        await _brandingRunner.Received(1).MigrateAsync(Arg.Any<CancellationToken>());
        await _notificationsRunner.Received(1).MigrateAsync(Arg.Any<CancellationToken>());
        await _announcementsRunner.Received(1).MigrateAsync(Arg.Any<CancellationToken>());
        await _storageRunner.Received(1).MigrateAsync(Arg.Any<CancellationToken>());
        await _apiKeysRunner.Received(1).MigrateAsync(Arg.Any<CancellationToken>());
        await _inquiriesRunner.Received(1).MigrateAsync(Arg.Any<CancellationToken>());
    }

    public void Dispose()
    {
        _worker.Dispose();
    }

    private async Task ExecuteWorkerAsync()
    {
        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(30));
        await _worker.StartAsync(cts.Token);
        await _worker.ExecuteTask!;
    }

    private IMigrationRunner CreateMockRunner(string name)
    {
        IMigrationRunner runner = Substitute.For<IMigrationRunner>();
        runner.ContextName.Returns(name);
        runner.MigrateAsync(Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                _migrationOrder.Add(name);
                return Task.CompletedTask;
            });
        return runner;
    }

    private int GetFirstFeatureModuleIndex()
    {
        string[] featureModules = ["Branding", "Notifications", "Announcements", "Storage", "ApiKeys", "Inquiries"];
        int firstIndex = int.MaxValue;
        foreach (string module in featureModules)
        {
            int index = _migrationOrder.IndexOf(module);
            if (index >= 0 && index < firstIndex)
            {
                firstIndex = index;
            }
        }
        return firstIndex == int.MaxValue ? -1 : firstIndex;
    }
}
