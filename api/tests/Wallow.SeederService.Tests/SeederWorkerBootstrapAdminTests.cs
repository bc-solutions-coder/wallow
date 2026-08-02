using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wallow.Identity.Application.Commands.BootstrapAdmin;
using Wallow.Identity.Application.Queries.IsSetupRequired;
using Wallow.Identity.Infrastructure.Options;
using Wallow.ServiceDefaults;

namespace Wallow.SeederService.Tests;

/// <summary>
/// Covers the seeder's admin bootstrap step. The regression under test: the step used to be
/// gated on <see cref="ISetupStatusChecker.IsSetupRequiredAsync"/>, which reports "setup done"
/// as soon as ANY user holds the admin role — so a stray admin-role user silently suppressed
/// the configured seed admin.
/// </summary>
public class SeederWorkerBootstrapAdminTests
{
    private const string SeedAdminEmail = "admin@wallow.dev";
    private const string SeedAdminPassword = "P@ssw0rd!";
    private const string SeedAdminFirstName = "Wallow";
    private const string SeedAdminLastName = "Admin";

    private readonly IBootstrapAdminService _bootstrapAdminService = Substitute.For<IBootstrapAdminService>();
    private readonly ISetupStatusChecker _setupStatusChecker = Substitute.For<ISetupStatusChecker>();
    private readonly IServiceScopeFactory _scopeFactory = Substitute.For<IServiceScopeFactory>();
    private readonly IHostApplicationLifetime _lifetime = Substitute.For<IHostApplicationLifetime>();
    private readonly RecordingLogger<SeederWorker> _logger = new();

    [Fact]
    public async Task BootstrapAdminAsync_WhenAnotherUserHoldsAdminRoleAndSeedAdminMissing_CreatesSeedAdmin()
    {
        Guid createdUserId = Guid.NewGuid();
        _setupStatusChecker.IsSetupRequiredAsync(Arg.Any<CancellationToken>()).Returns(false);
        _bootstrapAdminService.UserExistsAsync(SeedAdminEmail, Arg.Any<CancellationToken>()).Returns(false);
        _bootstrapAdminService
            .CreateUserAsync(SeedAdminEmail, SeedAdminPassword, SeedAdminFirstName, SeedAdminLastName, Arg.Any<CancellationToken>())
            .Returns(createdUserId);

        using SeederWorker worker = CreateWorker(ConfiguredAdmin());
        await using ServiceProvider sp = BuildServiceProvider();

        await worker.BootstrapAdminAsync(sp, CancellationToken.None);

        await _bootstrapAdminService.Received(1)
            .EnsureRoleExistsAsync("admin", Arg.Any<CancellationToken>());
        await _bootstrapAdminService.Received(1)
            .CreateUserAsync(SeedAdminEmail, SeedAdminPassword, SeedAdminFirstName, SeedAdminLastName, Arg.Any<CancellationToken>());
        await _bootstrapAdminService.Received(1)
            .AssignRoleAsync(createdUserId, "admin", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BootstrapAdminAsync_WhenAnotherUserHoldsAdminRoleAndSeedAdminMissing_LogsBootstrappedWithEmail()
    {
        _setupStatusChecker.IsSetupRequiredAsync(Arg.Any<CancellationToken>()).Returns(false);
        _bootstrapAdminService.UserExistsAsync(SeedAdminEmail, Arg.Any<CancellationToken>()).Returns(false);
        _bootstrapAdminService
            .CreateUserAsync(SeedAdminEmail, SeedAdminPassword, SeedAdminFirstName, SeedAdminLastName, Arg.Any<CancellationToken>())
            .Returns(Guid.NewGuid());

        using SeederWorker worker = CreateWorker(ConfiguredAdmin());
        await using ServiceProvider sp = BuildServiceProvider();

        await worker.BootstrapAdminAsync(sp, CancellationToken.None);

        _logger.Entries.Should().Contain(
            entry => entry.Message.Contains(SeedAdminEmail, StringComparison.Ordinal),
            "the seed admin must never be created or skipped without a log line naming it");
    }

    [Fact]
    public async Task BootstrapAdminAsync_WhenSeedAdminMissing_DoesNotConsultSetupStatusChecker()
    {
        _setupStatusChecker.IsSetupRequiredAsync(Arg.Any<CancellationToken>()).Returns(false);
        _bootstrapAdminService.UserExistsAsync(SeedAdminEmail, Arg.Any<CancellationToken>()).Returns(false);

        using SeederWorker worker = CreateWorker(ConfiguredAdmin());
        await using ServiceProvider sp = BuildServiceProvider();

        await worker.BootstrapAdminAsync(sp, CancellationToken.None);

        await _setupStatusChecker.DidNotReceive().IsSetupRequiredAsync(Arg.Any<CancellationToken>());
        await _bootstrapAdminService.Received(1).UserExistsAsync(SeedAdminEmail, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BootstrapAdminAsync_WhenSeedAdminAlreadyExists_LogsSkipNamingTheAdmin()
    {
        _setupStatusChecker.IsSetupRequiredAsync(Arg.Any<CancellationToken>()).Returns(true);
        _bootstrapAdminService.UserExistsAsync(SeedAdminEmail, Arg.Any<CancellationToken>()).Returns(true);

        using SeederWorker worker = CreateWorker(ConfiguredAdmin());
        await using ServiceProvider sp = BuildServiceProvider();

        await worker.BootstrapAdminAsync(sp, CancellationToken.None);

        _logger.Entries.Should().Contain(
            entry => entry.Message.Contains(SeedAdminEmail, StringComparison.Ordinal),
            "skipping an already-present seed admin must be reported, not silent");
    }

    [Fact]
    public async Task BootstrapAdminAsync_WhenSeedAdminAlreadyExists_LeavesTheExistingUserAlone()
    {
        _setupStatusChecker.IsSetupRequiredAsync(Arg.Any<CancellationToken>()).Returns(true);
        _bootstrapAdminService.UserExistsAsync(SeedAdminEmail, Arg.Any<CancellationToken>()).Returns(true);

        using SeederWorker worker = CreateWorker(ConfiguredAdmin());
        await using ServiceProvider sp = BuildServiceProvider();

        await worker.BootstrapAdminAsync(sp, CancellationToken.None);

        await _bootstrapAdminService.DidNotReceive()
            .CreateUserAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _bootstrapAdminService.DidNotReceive()
            .AssignRoleAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BootstrapAdminAsync_WhenNoAdminConfigured_LogsWarningAndTouchesNothing()
    {
        _setupStatusChecker.IsSetupRequiredAsync(Arg.Any<CancellationToken>()).Returns(true);

        using SeederWorker worker = CreateWorker(admin: null);
        await using ServiceProvider sp = BuildServiceProvider();

        await worker.BootstrapAdminAsync(sp, CancellationToken.None);

        _logger.Entries.Should().Contain(
            entry => entry.Level == LogLevel.Warning,
            "an unconfigured seed admin must be surfaced as a warning, not a silent no-op");
        await _bootstrapAdminService.DidNotReceive()
            .UserExistsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _bootstrapAdminService.DidNotReceive()
            .CreateUserAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BootstrapAdminAsync_WhenAdminPasswordMissing_LogsWarningAndTouchesNothing()
    {
        _setupStatusChecker.IsSetupRequiredAsync(Arg.Any<CancellationToken>()).Returns(true);

        AdminBootstrapOptions incomplete = new()
        {
            Email = SeedAdminEmail,
            Password = string.Empty,
            FirstName = SeedAdminFirstName,
            LastName = SeedAdminLastName
        };

        using SeederWorker worker = CreateWorker(incomplete);
        await using ServiceProvider sp = BuildServiceProvider();

        await worker.BootstrapAdminAsync(sp, CancellationToken.None);

        _logger.Entries.Should().Contain(
            entry => entry.Level == LogLevel.Warning,
            "a half-configured seed admin must be surfaced as a warning");
        await _bootstrapAdminService.DidNotReceive()
            .CreateUserAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BootstrapAdminAsync_OnFreshDatabase_CreatesSeedAdmin()
    {
        Guid createdUserId = Guid.NewGuid();
        _setupStatusChecker.IsSetupRequiredAsync(Arg.Any<CancellationToken>()).Returns(true);
        _bootstrapAdminService.UserExistsAsync(SeedAdminEmail, Arg.Any<CancellationToken>()).Returns(false);
        _bootstrapAdminService
            .CreateUserAsync(SeedAdminEmail, SeedAdminPassword, SeedAdminFirstName, SeedAdminLastName, Arg.Any<CancellationToken>())
            .Returns(createdUserId);

        using SeederWorker worker = CreateWorker(ConfiguredAdmin());
        await using ServiceProvider sp = BuildServiceProvider();

        await worker.BootstrapAdminAsync(sp, CancellationToken.None);

        await _bootstrapAdminService.Received(1)
            .EnsureRoleExistsAsync("admin", Arg.Any<CancellationToken>());
        await _bootstrapAdminService.Received(1)
            .CreateUserAsync(SeedAdminEmail, SeedAdminPassword, SeedAdminFirstName, SeedAdminLastName, Arg.Any<CancellationToken>());
        await _bootstrapAdminService.Received(1)
            .AssignRoleAsync(createdUserId, "admin", Arg.Any<CancellationToken>());
    }

    private static AdminBootstrapOptions ConfiguredAdmin() => new()
    {
        Email = SeedAdminEmail,
        Password = SeedAdminPassword,
        FirstName = SeedAdminFirstName,
        LastName = SeedAdminLastName
    };

    private SeederWorker CreateWorker(AdminBootstrapOptions? admin)
    {
        SeedOptions seedOptions = new() { Admin = admin };
        return new SeederWorker(_scopeFactory, Options.Create(seedOptions), _lifetime, new WorkerRunOutcome(), _logger);
    }

    private ServiceProvider BuildServiceProvider()
    {
        ServiceCollection services = new();
        services.AddSingleton(_bootstrapAdminService);
        services.AddSingleton(_setupStatusChecker);
        return services.BuildServiceProvider();
    }

}
