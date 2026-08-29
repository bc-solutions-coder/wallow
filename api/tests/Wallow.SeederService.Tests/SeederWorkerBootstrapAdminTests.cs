using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Wallow.Identity.Application.Commands.BootstrapAdmin;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Application.Queries.IsSetupRequired;
using Wallow.Identity.Infrastructure.Options;
using Wallow.ServiceDefaults;
using Wallow.Tests.Common;

namespace Wallow.SeederService.Tests;

/// <summary>
/// Covers the seeder's admin bootstrap step. The contract under test: the step delegates to the
/// same <see cref="BootstrapAdminHandler"/> the setup endpoint invokes (user + organization +
/// owner membership in one command), and it consults <see cref="ISetupStatusChecker"/> first —
/// a closed gate means a fully-provisioned administrator already exists (perhaps created through
/// the setup page), and a re-seed must not fight that outcome. The only seeder-specific work
/// left is the global-administrator claim, which no runtime endpoint grants.
/// </summary>
public class SeederWorkerBootstrapAdminTests
{
    private const string SeedAdminEmail = "admin@wallow.dev";
    private const string SeedAdminPassword = "P@ssw0rd!";
    private const string SeedAdminFirstName = "Wallow";
    private const string SeedAdminLastName = "Admin";
    private const string SeedAdminOrganizationName = "Wallow";

    private readonly IBootstrapAdminService _bootstrapAdminService = Substitute.For<IBootstrapAdminService>();
    private readonly IOrganizationService _organizationService = Substitute.For<IOrganizationService>();
    private readonly ISetupStatusChecker _setupStatusChecker = Substitute.For<ISetupStatusChecker>();
    private readonly IServiceScopeFactory _scopeFactory = Substitute.For<IServiceScopeFactory>();
    private readonly IHostApplicationLifetime _lifetime = Substitute.For<IHostApplicationLifetime>();
    private readonly RecordingLogger<SeederWorker> _logger = new();

    [Fact]
    public async Task BootstrapAdminAsync_OnFreshDatabase_CreatesFullyProvisionedAdmin()
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

        // The organization is what makes the user an administrator: passing the creator mints
        // the owner membership carrying the admin role. A bare user resolves no roles anywhere
        // and would leave the setup gate open forever.
        await _organizationService.Received(1)
            .CreateOrganizationAsync(SeedAdminOrganizationName, null, SeedAdminEmail, createdUserId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BootstrapAdminAsync_OnFreshDatabase_LogsBootstrappedWithEmail()
    {
        _setupStatusChecker.IsSetupRequiredAsync(Arg.Any<CancellationToken>()).Returns(true);
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
    public async Task BootstrapAdminAsync_WhenIsGlobalAdmin_GrantsTheClaimAfterBootstrap()
    {
        Guid createdUserId = Guid.NewGuid();
        _setupStatusChecker.IsSetupRequiredAsync(Arg.Any<CancellationToken>()).Returns(true);
        _bootstrapAdminService.UserExistsAsync(SeedAdminEmail, Arg.Any<CancellationToken>()).Returns(false);
        _bootstrapAdminService
            .CreateUserAsync(SeedAdminEmail, SeedAdminPassword, SeedAdminFirstName, SeedAdminLastName, Arg.Any<CancellationToken>())
            .Returns(createdUserId);
        _bootstrapAdminService.FindUserIdByEmailAsync(SeedAdminEmail, Arg.Any<CancellationToken>())
            .Returns(createdUserId);

        using SeederWorker worker = CreateWorker(ConfiguredAdmin(isGlobalAdmin: true));
        await using ServiceProvider sp = BuildServiceProvider();

        await worker.BootstrapAdminAsync(sp, CancellationToken.None);

        await _bootstrapAdminService.Received(1)
            .GrantGlobalAdminAsync(createdUserId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BootstrapAdminAsync_WhenNotGlobalAdmin_DoesNotGrantTheClaim()
    {
        _setupStatusChecker.IsSetupRequiredAsync(Arg.Any<CancellationToken>()).Returns(true);
        _bootstrapAdminService.UserExistsAsync(SeedAdminEmail, Arg.Any<CancellationToken>()).Returns(false);
        _bootstrapAdminService
            .CreateUserAsync(SeedAdminEmail, SeedAdminPassword, SeedAdminFirstName, SeedAdminLastName, Arg.Any<CancellationToken>())
            .Returns(Guid.NewGuid());

        using SeederWorker worker = CreateWorker(ConfiguredAdmin());
        await using ServiceProvider sp = BuildServiceProvider();

        await worker.BootstrapAdminAsync(sp, CancellationToken.None);

        await _bootstrapAdminService.DidNotReceive()
            .GrantGlobalAdminAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BootstrapAdminAsync_WhenSetupAlreadyComplete_SkipsAndLogsNamingTheAdmin()
    {
        _setupStatusChecker.IsSetupRequiredAsync(Arg.Any<CancellationToken>()).Returns(false);

        using SeederWorker worker = CreateWorker(ConfiguredAdmin());
        await using ServiceProvider sp = BuildServiceProvider();

        await worker.BootstrapAdminAsync(sp, CancellationToken.None);

        // A closed gate means a fully-provisioned administrator exists — possibly one a human
        // created through the setup page with a different email. Re-seeding must not create a
        // second admin or touch the existing one.
        await _bootstrapAdminService.DidNotReceive()
            .CreateUserAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _bootstrapAdminService.DidNotReceive()
            .GrantGlobalAdminAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        _logger.Entries.Should().Contain(
            entry => entry.Message.Contains(SeedAdminEmail, StringComparison.Ordinal),
            "skipping the configured seed admin must be reported, not silent");
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

        // The gate is open yet the account exists: a half-bootstrapped user with no admin
        // membership. Creating on top of it would fail; the step leaves it for a human.
        await _bootstrapAdminService.DidNotReceive()
            .CreateUserAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _organizationService.DidNotReceive()
            .CreateOrganizationAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
        await _bootstrapAdminService.DidNotReceive()
            .GrantGlobalAdminAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
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
        await _setupStatusChecker.DidNotReceive().IsSetupRequiredAsync(Arg.Any<CancellationToken>());
        await _bootstrapAdminService.DidNotReceive()
            .CreateUserAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("", SeedAdminPassword, SeedAdminOrganizationName)]
    [InlineData(SeedAdminEmail, "", SeedAdminOrganizationName)]
    [InlineData(SeedAdminEmail, SeedAdminPassword, "")]
    public async Task BootstrapAdminAsync_WhenAdminHalfConfigured_LogsWarningAndTouchesNothing(
        string email, string password, string organizationName)
    {
        _setupStatusChecker.IsSetupRequiredAsync(Arg.Any<CancellationToken>()).Returns(true);

        AdminBootstrapOptions incomplete = new()
        {
            Email = email,
            Password = password,
            FirstName = SeedAdminFirstName,
            LastName = SeedAdminLastName,
            OrganizationName = organizationName
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
    public async Task BootstrapAdminAsync_WhenGlobalAdminUserCannotBeFoundAfterBootstrap_Throws()
    {
        _setupStatusChecker.IsSetupRequiredAsync(Arg.Any<CancellationToken>()).Returns(true);
        _bootstrapAdminService.UserExistsAsync(SeedAdminEmail, Arg.Any<CancellationToken>()).Returns(false);
        _bootstrapAdminService
            .CreateUserAsync(SeedAdminEmail, SeedAdminPassword, SeedAdminFirstName, SeedAdminLastName, Arg.Any<CancellationToken>())
            .Returns(Guid.NewGuid());
        _bootstrapAdminService.FindUserIdByEmailAsync(SeedAdminEmail, Arg.Any<CancellationToken>())
            .Returns((Guid?)null);

        using SeederWorker worker = CreateWorker(ConfiguredAdmin(isGlobalAdmin: true));
        await using ServiceProvider sp = BuildServiceProvider();

        Func<Task> act = () => worker.BootstrapAdminAsync(sp, CancellationToken.None);

        // The seeder exits non-zero on a thrown step: silently skipping the grant would leave a
        // deployment that believes it seeded a global admin without one.
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    private static AdminBootstrapOptions ConfiguredAdmin(bool isGlobalAdmin = false) => new()
    {
        Email = SeedAdminEmail,
        Password = SeedAdminPassword,
        FirstName = SeedAdminFirstName,
        LastName = SeedAdminLastName,
        OrganizationName = SeedAdminOrganizationName,
        IsGlobalAdmin = isGlobalAdmin
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
        // A REAL handler over the substituted services, exactly as the seeder wires it: the step's
        // contract is "delegate to the same command the setup endpoint runs", so the tests assert
        // through the handler rather than substituting it away.
        services.AddSingleton(new BootstrapAdminHandler(
            _bootstrapAdminService,
            _organizationService,
            NullLogger<BootstrapAdminHandler>.Instance));
        return services.BuildServiceProvider();
    }
}
