using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Infrastructure.Persistence;
using Wallow.Identity.Infrastructure.Services;
using Wallow.Shared.Kernel.Identity;

namespace Wallow.Identity.Tests.Infrastructure;

public sealed class OrganizationMfaPolicyServiceTests : IDisposable
{
    private readonly IdentityDbContext _dbContext;
    private readonly UserManager<WallowUser> _userManager;
    private readonly FakeTimeProvider _timeProvider;
    private readonly ILogger<OrganizationMfaPolicyService> _logger = Substitute.For<ILogger<OrganizationMfaPolicyService>>();
    private readonly OrganizationMfaPolicyService _sut;
    private readonly Guid _tenantId = Guid.NewGuid();

    public OrganizationMfaPolicyServiceTests()
    {
        DbContextOptions<IdentityDbContext> opts = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        IDataProtectionProvider dp = DataProtectionProvider.Create("test");
        _dbContext = new IdentityDbContext(opts, dp);
        _dbContext.SetTenant(new TenantId(_tenantId));

        IUserStore<WallowUser> userStore = Substitute.For<IUserStore<WallowUser>>();
        _userManager = Substitute.For<UserManager<WallowUser>>(
            userStore, null, null, null, null, null, null, null, null);

        _timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
        _sut = new OrganizationMfaPolicyService(_dbContext, _userManager, _timeProvider, _logger);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _userManager.Dispose();
    }

    [Fact]
    public async Task CheckAsync_UserNotFound_ReturnsNotRequired()
    {
        _userManager.FindByIdAsync(Arg.Any<string>()).Returns((WallowUser?)null);

        OrgMfaPolicyResult result = await _sut.CheckAsync(Guid.NewGuid(), CancellationToken.None);

        result.RequiresMfa.Should().BeFalse();
        result.IsInGracePeriod.Should().BeFalse();
    }

    [Fact]
    public async Task CheckAsync_UserAlreadyHasMfaEnabled_ReturnsNotRequired()
    {
        WallowUser user = WallowUser.Create(_tenantId, "Mfa", "Enabled", "mfa@t.com", TimeProvider.System);
        user.EnableMfa("totp", "encrypted-secret");
        _userManager.FindByIdAsync(user.Id.ToString()).Returns(user);

        OrgMfaPolicyResult result = await _sut.CheckAsync(user.Id, CancellationToken.None);

        result.RequiresMfa.Should().BeFalse();
    }

    [Fact]
    public async Task CheckAsync_UserNotInOrg_ReturnsNotRequired()
    {
        WallowUser user = WallowUser.Create(_tenantId, "No", "Org", "noorg@t.com", TimeProvider.System);
        _userManager.FindByIdAsync(user.Id.ToString()).Returns(user);

        OrgMfaPolicyResult result = await _sut.CheckAsync(user.Id, CancellationToken.None);

        result.RequiresMfa.Should().BeFalse();
    }

    [Fact]
    public async Task CheckAsync_OrgDoesNotRequireMfa_ReturnsNotRequired()
    {
        WallowUser user = WallowUser.Create(_tenantId, "No", "Mfa", "nomfa@t.com", TimeProvider.System);
        _userManager.FindByIdAsync(user.Id.ToString()).Returns(user);

        Organization org = Organization.Create(
            TenantId.Create(_tenantId), "Test Org", "test-org", user.Id, TimeProvider.System);
        _dbContext.Organizations.Add(org);

        OrganizationSettings settings = OrganizationSettings.Create(
            org.Id, TenantId.Create(_tenantId), requireMfa: false,
            allowPasswordlessLogin: false, mfaGracePeriodDays: 0,
            user.Id, TimeProvider.System);
        _dbContext.OrganizationSettings.Add(settings);
        await _dbContext.SaveChangesAsync();

        // Add membership via the organization's Members collection
        org.AddMember(user.Id, "member", user.Id, TimeProvider.System);
        await _dbContext.SaveChangesAsync();

        OrgMfaPolicyResult result = await _sut.CheckAsync(user.Id, CancellationToken.None);

        result.RequiresMfa.Should().BeFalse();
    }

    [Fact]
    public async Task CheckAsync_OrgRequiresMfa_UserNoGrace_ReturnsRequiredNotInGrace()
    {
        WallowUser user = WallowUser.Create(_tenantId, "Need", "Mfa", "needmfa@t.com", TimeProvider.System);
        _userManager.FindByIdAsync(user.Id.ToString()).Returns(user);

        Organization org = Organization.Create(
            TenantId.Create(_tenantId), "MFA Org", "mfa-org", user.Id, TimeProvider.System);
        _dbContext.Organizations.Add(org);

        OrganizationSettings settings = OrganizationSettings.Create(
            org.Id, TenantId.Create(_tenantId), requireMfa: true,
            allowPasswordlessLogin: false, mfaGracePeriodDays: 0,
            user.Id, TimeProvider.System);
        _dbContext.OrganizationSettings.Add(settings);

        org.AddMember(user.Id, "member", user.Id, TimeProvider.System);
        await _dbContext.SaveChangesAsync();

        OrgMfaPolicyResult result = await _sut.CheckAsync(user.Id, CancellationToken.None);

        result.RequiresMfa.Should().BeTrue();
        result.IsInGracePeriod.Should().BeFalse();
    }

    [Fact]
    public async Task CheckAsync_OrgRequiresMfa_UserInGrace_ReturnsRequiredAndInGrace()
    {
        WallowUser user = WallowUser.Create(_tenantId, "Grace", "Period", "grace@t.com", TimeProvider.System);
        user.SetMfaGraceDeadline(DateTimeOffset.UtcNow.AddDays(7));
        _userManager.FindByIdAsync(user.Id.ToString()).Returns(user);

        Organization org = Organization.Create(
            TenantId.Create(_tenantId), "Grace Org", "grace-org", user.Id, TimeProvider.System);
        _dbContext.Organizations.Add(org);

        OrganizationSettings settings = OrganizationSettings.Create(
            org.Id, TenantId.Create(_tenantId), requireMfa: true,
            allowPasswordlessLogin: false, mfaGracePeriodDays: 14,
            user.Id, TimeProvider.System);
        _dbContext.OrganizationSettings.Add(settings);

        org.AddMember(user.Id, "member", user.Id, TimeProvider.System);
        await _dbContext.SaveChangesAsync();

        OrgMfaPolicyResult result = await _sut.CheckAsync(user.Id, CancellationToken.None);

        result.RequiresMfa.Should().BeTrue();
        result.IsInGracePeriod.Should().BeTrue();
    }
}
