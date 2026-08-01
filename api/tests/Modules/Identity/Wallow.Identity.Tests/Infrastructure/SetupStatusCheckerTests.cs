using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Domain.Identity;
using Wallow.Identity.Infrastructure.Persistence;
using Wallow.Identity.Infrastructure.Services;

namespace Wallow.Identity.Tests.Infrastructure;

/// <summary>
/// The gate on the <c>[AllowAnonymous]</c> setup endpoints. "An administrator exists" is an
/// Active membership holding an AdminAccess-granting role — a role row on its own leaves setup
/// open, and a membership holding only a baseline role does too, because anyone reaching
/// POST /identity/setup/admin while this reports true creates an administrator unauthenticated.
/// </summary>
public sealed class SetupStatusCheckerTests : IDisposable
{
    private readonly IdentityDbContext _dbContext;
    private readonly SetupStatusChecker _sut;

    public SetupStatusCheckerTests()
    {
        DbContextOptions<IdentityDbContext> options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        IDataProtectionProvider dataProtectionProvider = DataProtectionProvider.Create("Wallow.Identity.Tests");
        _dbContext = new IdentityDbContext(options, dataProtectionProvider);
        _sut = new SetupStatusChecker(_dbContext);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        GC.SuppressFinalize(this);
    }

    private async Task<Guid> SeedRoleAsync(string name)
    {
        WallowRole role = new() { Id = Guid.NewGuid(), Name = name, NormalizedName = name.ToUpperInvariant() };
        _dbContext.Roles.Add(role);
        await _dbContext.SaveChangesAsync();
        return role.Id;
    }

    private async Task SeedMembershipAsync(Guid roleId, bool suspended = false)
    {
        Guid userId = Guid.NewGuid();
        Membership membership = Membership.Enroll(
            userId, OrganizationId.New(), roleId, TimeProvider.System);

        if (suspended)
        {
            membership.Suspend(userId, TimeProvider.System);
        }

        _dbContext.Memberships.Add(membership);
        await _dbContext.SaveChangesAsync();
    }

    [Fact]
    public async Task IsSetupRequiredAsync_WhenNoRolesExist_ReturnsTrue()
    {
        bool required = await _sut.IsSetupRequiredAsync();

        required.Should().BeTrue();
    }

    [Fact]
    public async Task IsSetupRequiredAsync_WhenAdminRoleExistsButNobodyHoldsIt_ReturnsTrue()
    {
        await SeedRoleAsync("admin");

        bool required = await _sut.IsSetupRequiredAsync();

        required.Should().BeTrue();
    }

    [Fact]
    public async Task IsSetupRequiredAsync_WhenSeededAdminMembershipExists_ReturnsFalse()
    {
        Guid adminRoleId = await SeedRoleAsync("admin");
        await SeedMembershipAsync(adminRoleId);

        bool required = await _sut.IsSetupRequiredAsync();

        required.Should().BeFalse();
    }

    [Fact]
    public async Task IsSetupRequiredAsync_WhenOnlyBaselineMembershipExists_ReturnsTrue()
    {
        await SeedRoleAsync("admin");
        Guid userRoleId = await SeedRoleAsync("user");
        await SeedMembershipAsync(userRoleId);

        bool required = await _sut.IsSetupRequiredAsync();

        required.Should().BeTrue();
    }

    [Fact]
    public async Task IsSetupRequiredAsync_WhenTheOnlyAdminMembershipIsSuspended_ReturnsTrue()
    {
        Guid adminRoleId = await SeedRoleAsync("admin");
        await SeedMembershipAsync(adminRoleId, suspended: true);

        bool required = await _sut.IsSetupRequiredAsync();

        required.Should().BeTrue();
    }
}
