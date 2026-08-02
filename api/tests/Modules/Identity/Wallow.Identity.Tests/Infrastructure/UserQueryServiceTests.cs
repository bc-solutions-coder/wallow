using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Domain.Identity;
using Wallow.Identity.Infrastructure.Persistence;
using Wallow.Identity.Infrastructure.Services;
using Wallow.Shared.Contracts.Identity;

namespace Wallow.Identity.Tests.Infrastructure;

/// <summary>
/// The user list is one organization's list: the controller resolves the ambient tenant and hands
/// it in. Role names on it therefore have to come from the membership of THAT organization, not
/// from a directory that spans all of them.
/// </summary>
public sealed class UserQueryServiceTests : IDisposable
{
    private readonly UserManager<WallowUser> _userManager;
    private readonly IdentityDbContext _dbContext;
    private readonly FakeTimeProvider _timeProvider = new(DateTimeOffset.UtcNow);
    private readonly UserQueryService _sut;
    private readonly Guid _organizationId = Guid.NewGuid();

    public UserQueryServiceTests()
    {
        IUserStore<WallowUser> userStore = Substitute.For<IUserStore<WallowUser>>();
        _userManager = Substitute.For<UserManager<WallowUser>>(
            userStore, null, null, null, null, null, null, null, null);

        DbContextOptions<IdentityDbContext> dbOptions = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new IdentityDbContext(
            dbOptions,
            Microsoft.AspNetCore.DataProtection.DataProtectionProvider.Create("test"));

        _sut = new UserQueryService(
            _userManager,
            _dbContext,
            NullLoggerFactory.Instance.CreateLogger<UserQueryService>());
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _userManager.Dispose();
    }

    private async Task<WallowUser> SeedUserAsync(string email)
    {
        WallowUser user = WallowUser.Create("Test", "User", email, _timeProvider);
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();
        return user;
    }

    private async Task<Guid> SeedRoleAsync(string name)
    {
        WallowRole role = new()
        {
            Id = Guid.NewGuid(),
            Name = name,
            NormalizedName = name.ToUpperInvariant(),
            TenantId = Guid.Empty
        };

        _dbContext.Roles.Add(role);
        await _dbContext.SaveChangesAsync();

        return role.Id;
    }

    private async Task<Membership> SeedMembershipAsync(Guid userId, Guid organizationId, Guid roleId)
    {
        Membership membership = Membership.Enroll(
            userId, OrganizationId.Create(organizationId), roleId, _timeProvider);

        _dbContext.Memberships.Add(membership);
        await _dbContext.SaveChangesAsync();

        return membership;
    }

    [Fact]
    public async Task SearchUsersAsync_ReportsRolesGrantedByTheOrganizationBeingSearched()
    {
        WallowUser user = await SeedUserAsync("admin@test.com");
        Guid adminRoleId = await SeedRoleAsync("admin");
        await SeedMembershipAsync(user.Id, _organizationId, adminRoleId);

        UserSearchPageResult result = await _sut.SearchUsersAsync(_organizationId, null, 0, 20);

        result.Items.Should().ContainSingle()
            .Which.Roles.Should().Contain("admin");
    }

    [Fact]
    public async Task SearchUsersAsync_DoesNotReportRolesGrantedByAnotherOrganization()
    {
        WallowUser user = await SeedUserAsync("elsewhere@test.com");
        Guid adminRoleId = await SeedRoleAsync("admin");
        await SeedMembershipAsync(user.Id, Guid.NewGuid(), adminRoleId);

        UserSearchPageResult result = await _sut.SearchUsersAsync(_organizationId, null, 0, 20);

        // The row still appears — scoping the list itself is a separate question — but claiming
        // an administrator role this user does not hold here would be a lie on the screen.
        result.Items.Should().ContainSingle()
            .Which.Roles.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchUsersAsync_DoesNotReportRolesFromAMembershipThatIsNotActive()
    {
        WallowUser user = await SeedUserAsync("suspended@test.com");
        Guid adminRoleId = await SeedRoleAsync("admin");
        Membership membership = await SeedMembershipAsync(user.Id, _organizationId, adminRoleId);
        membership.Suspend(Guid.NewGuid(), _timeProvider);
        await _dbContext.SaveChangesAsync();

        UserSearchPageResult result = await _sut.SearchUsersAsync(_organizationId, null, 0, 20);

        result.Items.Should().ContainSingle()
            .Which.Roles.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchUsersAsync_ResolvesRolesForEveryUserOnThePage()
    {
        WallowUser first = await SeedUserAsync("a@test.com");
        WallowUser second = await SeedUserAsync("b@test.com");
        Guid adminRoleId = await SeedRoleAsync("admin");
        Guid userRoleId = await SeedRoleAsync("user");
        await SeedMembershipAsync(first.Id, _organizationId, adminRoleId);
        await SeedMembershipAsync(second.Id, _organizationId, userRoleId);

        UserSearchPageResult result = await _sut.SearchUsersAsync(_organizationId, null, 0, 20);

        result.Items.Should().HaveCount(2);
        result.Items.Single(i => i.Id == first.Id).Roles.Should().Contain("admin");
        result.Items.Single(i => i.Id == second.Id).Roles.Should().Contain("user");
    }
}
