using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Infrastructure.Persistence;
using Wallow.Identity.Infrastructure.Repositories;
using Wallow.Shared.Kernel.Identity;

namespace Wallow.Identity.Tests.Infrastructure;

/// <summary>
/// <see cref="OrganizationRepository.GetByUserIdAsync"/> — the source feeding the
/// AuthorizationController membership gate — resolves from Membership rows, not from a user's
/// <c>WallowUser.TenantId</c> (the home tenant set at registration) and not from any
/// tenant-equality on the caller.
/// </summary>
public sealed class MembershipReadModelTests : IDisposable
{
    private readonly IdentityDbContext _dbContext;

    public MembershipReadModelTests()
    {
        DbContextOptions<IdentityDbContext> options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        IDataProtectionProvider dataProtectionProvider = DataProtectionProvider.Create("Wallow.Identity.Tests");
        _dbContext = new IdentityDbContext(options, dataProtectionProvider);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task GetByUserIdAsync_ResolvesFromMemberships_NotFromCallerTenant()
    {
        Guid userId = Guid.NewGuid();

        Organization memberOrg = Organization.Create(
            TenantId.Create(Guid.NewGuid()), "Member Org", "member-org", Guid.NewGuid(), TimeProvider.System);

        Organization otherOrg = Organization.Create(
            TenantId.Create(Guid.NewGuid()), "Other Org", "other-org", Guid.NewGuid(), TimeProvider.System);

        // Ambient tenant is an unrelated value — membership must NOT depend on tenant equality.
        _dbContext.SetTenant(new TenantId(Guid.NewGuid()));
        _dbContext.Organizations.AddRange(memberOrg, otherOrg);
        _dbContext.Memberships.Add(Membership.Enroll(userId, memberOrg.Id, Guid.NewGuid(), TimeProvider.System));
        await _dbContext.SaveChangesAsync();

        OrganizationRepository repo = new(_dbContext);
        List<Organization> result = await repo.GetByUserIdAsync(userId);

        result.Should().ContainSingle(o => o.Id == memberOrg.Id);
        result.Should().NotContain(o => o.Id == otherOrg.Id);
    }

    [Fact]
    public async Task GetByUserIdAsync_ReturnsEmpty_WhenUserHasNoMembership()
    {
        Organization org = Organization.Create(
            TenantId.Create(Guid.NewGuid()), "Org", "org", Guid.NewGuid(), TimeProvider.System);

        _dbContext.SetTenant(new TenantId(org.Id.Value));
        _dbContext.Organizations.Add(org);
        await _dbContext.SaveChangesAsync();

        OrganizationRepository repo = new(_dbContext);
        List<Organization> result = await repo.GetByUserIdAsync(Guid.NewGuid());

        result.Should().BeEmpty();
    }
}
