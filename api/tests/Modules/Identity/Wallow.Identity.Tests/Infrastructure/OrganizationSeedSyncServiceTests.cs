using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Wallow.Identity.Application.DTOs;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Enums;
using Wallow.Identity.Infrastructure.Options;
using Wallow.Identity.Infrastructure.Persistence;
using Wallow.Identity.Infrastructure.Services;
using Wallow.Shared.Kernel.Identity;

namespace Wallow.Identity.Tests.Infrastructure;

/// <summary>
/// Checks declared enrollment policy updates and preservation of settings omitted from the seed.
/// </summary>
public sealed class OrganizationSeedSyncServiceTests : IDisposable
{
    private readonly IOrganizationService _organizationService;
    private readonly IdentityDbContext _dbContext;
    private readonly SeedOrganizationOptions _options;
    private readonly OrganizationSeedSyncService _sut;
    private Guid? _tenantDuringSettingsRead;

    public OrganizationSeedSyncServiceTests()
    {
        DbContextOptions<IdentityDbContext> dbOptions = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new IdentityDbContext(dbOptions, DataProtectionProvider.Create("Wallow.Identity.Tests"));

        _organizationService = Substitute.For<IOrganizationService>();
        _options = new SeedOrganizationOptions();
        _sut = new OrganizationSeedSyncService(
            _organizationService,
            _dbContext,
            Options.Create(_options),
            NullLogger<OrganizationSeedSyncService>.Instance);
    }

    public void Dispose() => _dbContext.Dispose();

    [Fact]
    public async Task SyncAsync_ForAnExistingOrganization_AppliesTheDeclaredPolicy()
    {
        Guid organizationId = ExistingOrganization("Wallow");
        Settings(organizationId, EnrollmentPolicy.InviteOnly);
        Declare("Wallow", EnrollmentPolicy.Open);

        await _sut.SyncAsync(CancellationToken.None);

        await _organizationService.Received(1).UpdateEnrollmentAsync(
            organizationId,
            EnrollmentPolicy.Open,
            Arg.Any<string?>(),
            Arg.Any<Guid?>(),
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncAsync_MatchesTheOrganizationNameCaseInsensitively()
    {
        Guid organizationId = ExistingOrganization("Wallow");
        Settings(organizationId, EnrollmentPolicy.InviteOnly);
        Declare("wallow", EnrollmentPolicy.Open);

        await _sut.SyncAsync(CancellationToken.None);

        await _organizationService.DidNotReceive().CreateOrganizationAsync(
            Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
        await _organizationService.Received(1).UpdateEnrollmentAsync(
            organizationId, EnrollmentPolicy.Open, Arg.Any<string?>(), Arg.Any<Guid?>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncAsync_ForAnOrganizationThatDoesNotExist_CreatesIt()
    {
        Guid organizationId = Guid.NewGuid();
        _organizationService.GetOrganizationsAsync("Contoso", Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([]);
        _organizationService.CreateOrganizationAsync("Contoso", Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(organizationId);
        Settings(organizationId, EnrollmentPolicy.InviteOnly);
        Declare("Contoso", EnrollmentPolicy.RequestApproval);

        await _sut.SyncAsync(CancellationToken.None);

        await _organizationService.Received(1).UpdateEnrollmentAsync(
            organizationId, EnrollmentPolicy.RequestApproval, Arg.Any<string?>(), Arg.Any<Guid?>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncAsync_WithNoDeclaredPolicy_LeavesEnrollmentAlone()
    {
        // An omitted policy must leave existing enrollment settings unchanged.
        Guid organizationId = ExistingOrganization("Wallow");
        Settings(organizationId, EnrollmentPolicy.InviteOnly);
        _options.Organizations.Add(new SeedOrganizationDefinition { Name = "Wallow" });

        await _sut.SyncAsync(CancellationToken.None);

        await _organizationService.DidNotReceive().UpdateEnrollmentAsync(
            Arg.Any<Guid>(), Arg.Any<EnrollmentPolicy>(), Arg.Any<string?>(), Arg.Any<Guid?>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncAsync_PreservesTheDefaultRoleTheOrganizationAlreadyCarries()
    {
        Guid organizationId = ExistingOrganization("Wallow");
        Guid defaultRoleId = Guid.NewGuid();
        Settings(organizationId, EnrollmentPolicy.InviteOnly, defaultRoleId: defaultRoleId);
        Declare("Wallow", EnrollmentPolicy.Open);

        await _sut.SyncAsync(CancellationToken.None);

        await _organizationService.Received(1).UpdateEnrollmentAsync(
            organizationId, EnrollmentPolicy.Open, Arg.Any<string?>(), defaultRoleId, Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncAsync_WithoutAnAccessRequestEmail_KeepsTheConfiguredAddress()
    {
        Guid organizationId = ExistingOrganization("Wallow");
        Settings(organizationId, EnrollmentPolicy.InviteOnly, accessRequestEmail: "owner@contoso.test");
        Declare("Wallow", EnrollmentPolicy.RequestApproval);

        await _sut.SyncAsync(CancellationToken.None);

        await _organizationService.Received(1).UpdateEnrollmentAsync(
            organizationId, EnrollmentPolicy.RequestApproval, "owner@contoso.test", Arg.Any<Guid?>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncAsync_WithAnAccessRequestEmail_OverwritesTheConfiguredAddress()
    {
        Guid organizationId = ExistingOrganization("Wallow");
        Settings(organizationId, EnrollmentPolicy.InviteOnly, accessRequestEmail: "owner@contoso.test");
        Declare("Wallow", EnrollmentPolicy.RequestApproval, accessRequestEmail: "admin@wallow.dev");

        await _sut.SyncAsync(CancellationToken.None);

        await _organizationService.Received(1).UpdateEnrollmentAsync(
            organizationId, EnrollmentPolicy.RequestApproval, "admin@wallow.dev", Arg.Any<Guid?>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncAsync_WhenTheOrganizationAlreadyMatches_WritesNothing()
    {
        Guid organizationId = ExistingOrganization("Wallow");
        Settings(organizationId, EnrollmentPolicy.Open, accessRequestEmail: "admin@wallow.dev");
        Declare("Wallow", EnrollmentPolicy.Open, accessRequestEmail: "admin@wallow.dev");

        await _sut.SyncAsync(CancellationToken.None);

        await _organizationService.DidNotReceive().UpdateEnrollmentAsync(
            Arg.Any<Guid>(), Arg.Any<EnrollmentPolicy>(), Arg.Any<string?>(), Arg.Any<Guid?>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncAsync_ForANamelessOrganization_ThrowsBeforeTouchingAnything()
    {
        _options.Organizations.Add(new SeedOrganizationDefinition { EnrollmentPolicy = EnrollmentPolicy.Open });

        Func<Task> act = () => _sut.SyncAsync(CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        await _organizationService.DidNotReceive().CreateOrganizationAsync(
            Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncAsync_ReadsSettingsScopedToTheOrganizationAndLeavesNoScopeBehind()
    {
        // Settings access must run under the target organization tenant, then restore the prior context.
        Guid organizationId = ExistingOrganization("Wallow");
        Settings(organizationId, EnrollmentPolicy.InviteOnly);
        Declare("Wallow", EnrollmentPolicy.Open);

        Guid? tenantDuringWrite = null;
        _organizationService
            .When(s => s.UpdateEnrollmentAsync(
                Arg.Any<Guid>(), Arg.Any<EnrollmentPolicy>(), Arg.Any<string?>(), Arg.Any<Guid?>(),
                Arg.Any<Guid>(), Arg.Any<CancellationToken>()))
            .Do(_ => tenantDuringWrite = _dbContext.CurrentTenantId.Value);

        await _sut.SyncAsync(CancellationToken.None);

        _tenantDuringSettingsRead.Should().Be(organizationId);
        tenantDuringWrite.Should().Be(organizationId);
        _dbContext.CurrentTenantId.Should().Be(default(TenantId));
    }

    private Guid ExistingOrganization(string name)
    {
        Guid organizationId = Guid.NewGuid();
        _organizationService
            .GetOrganizationsAsync(Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([new OrganizationDto(organizationId, name, name.ToLowerInvariant(), 0)]);

        return organizationId;
    }

    private void Settings(
        Guid organizationId,
        EnrollmentPolicy policy,
        string? accessRequestEmail = null,
        Guid? defaultRoleId = null)
    {
        _organizationService.GetSettingsAsync(organizationId, Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                _tenantDuringSettingsRead = _dbContext.CurrentTenantId.Value;
                return new OrganizationSettingsDto(
                    organizationId,
                    RequireMfa: false,
                    AllowPasswordlessLogin: true,
                    MfaGracePeriodDays: 7,
                    policy,
                    accessRequestEmail,
                    defaultRoleId);
            });
    }

    private void Declare(string name, EnrollmentPolicy policy, string? accessRequestEmail = null)
    {
        _options.Organizations.Add(new SeedOrganizationDefinition
        {
            Name = name,
            EnrollmentPolicy = policy,
            AccessRequestEmail = accessRequestEmail
        });
    }
}
