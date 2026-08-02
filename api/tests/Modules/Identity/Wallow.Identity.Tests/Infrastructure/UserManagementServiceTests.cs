using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Wallow.Identity.Application.DTOs;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Domain.Identity;
using Wallow.Identity.Infrastructure.Persistence;
using Wallow.Identity.Infrastructure.Services;
using Wallow.Shared.Contracts.Identity.Events;
using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.MultiTenancy;
using Wolverine;

namespace Wallow.Identity.Tests.Infrastructure;

public sealed class UserManagementServiceTests : IDisposable
{
    private readonly UserManager<WallowUser> _userManager;
    private readonly RoleManager<WallowRole> _roleManager;
    private readonly IMessageBus _messageBus;
    private readonly ITenantContext _tenantContext;
    private readonly FakeTimeProvider _timeProvider;
    private readonly IdentityDbContext _dbContext;
    private readonly IMembershipRepository _membershipRepository;
    private readonly UserManagementService _sut;
    private readonly Guid _organizationId;

    public UserManagementServiceTests()
    {
        IUserStore<WallowUser> userStore = Substitute.For<IUserStore<WallowUser>>();
        _userManager = Substitute.For<UserManager<WallowUser>>(
            userStore, null, null, null, null, null, null, null, null);

        IRoleStore<WallowRole> roleStore = Substitute.For<IRoleStore<WallowRole>>();
        _roleManager = Substitute.For<RoleManager<WallowRole>>(
            roleStore, null, null, null, null);

        _messageBus = Substitute.For<IMessageBus>();
        _tenantContext = Substitute.For<ITenantContext>();
        _organizationId = Guid.NewGuid();
        _tenantContext.TenantId.Returns(new TenantId(_organizationId));
        _timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
        _membershipRepository = Substitute.For<IMembershipRepository>();

        DbContextOptions<IdentityDbContext> dbOptions = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new IdentityDbContext(dbOptions,
            Microsoft.AspNetCore.DataProtection.DataProtectionProvider.Create("test"));
        _sut = new UserManagementService(
            _userManager,
            _roleManager,
            _dbContext,
            _membershipRepository,
            _messageBus,
            _tenantContext,
            _timeProvider,
            NullLoggerFactory.Instance.CreateLogger<UserManagementService>());
    }

    /// <summary>
    /// Role ids are resolved out of the catalog by normalized name, so a role a test wants to
    /// grant has to exist as a row before the grant is attempted.
    /// </summary>
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

    private Membership SeedMembership(Guid userId, Guid organizationId, Guid defaultRoleId)
    {
        Membership membership = Membership.Enroll(
            userId, OrganizationId.Create(organizationId), defaultRoleId, _timeProvider);

        _membershipRepository.GetAsync(userId, organizationId, Arg.Any<CancellationToken>())
            .Returns(membership);

        return membership;
    }

    /// <summary>
    /// The batch role reader queries memberships directly rather than through the repository,
    /// because a user list resolves many users in one round trip. Tests that exercise it need a
    /// real row, not a stubbed repository answer.
    /// </summary>
    private async Task<Membership> PersistMembershipAsync(Guid userId, Guid organizationId, Guid roleId)
    {
        Membership membership = Membership.Enroll(
            userId, OrganizationId.Create(organizationId), roleId, _timeProvider);

        _dbContext.Memberships.Add(membership);
        await _dbContext.SaveChangesAsync();

        return membership;
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _userManager.Dispose();
        _roleManager.Dispose();
    }

    [Fact]
    public async Task CreateUserAsync_WithPassword_CreatesUserAndPublishesEvent()
    {
        await SeedRoleAsync("user");
        _userManager.CreateAsync(Arg.Any<WallowUser>(), Arg.Any<string>())
            .Returns(IdentityResult.Success);

        Guid result = await _sut.CreateUserAsync("john@test.com", "John", "Doe", "Password123!");

        result.Should().NotBeEmpty();
        await _userManager.Received(1).CreateAsync(Arg.Any<WallowUser>(), "Password123!");
        await _messageBus.Received(1).PublishAsync(Arg.Any<UserRegisteredEvent>());
    }

    [Fact]
    public async Task CreateUserAsync_EnrollsTheNewUserInTheAdministeredOrganization()
    {
        Guid userRoleId = await SeedRoleAsync("user");
        _userManager.CreateAsync(Arg.Any<WallowUser>(), Arg.Any<string>())
            .Returns(IdentityResult.Success);

        Guid userId = await _sut.CreateUserAsync("john@test.com", "John", "Doe", "Password123!");

        _membershipRepository.Received(1).Add(Arg.Is<Membership>(m =>
            m.UserId == userId
            && m.OrganizationId.Value == _organizationId
            && m.IsActive
            && m.RoleIds.Contains(userRoleId)));
    }

    [Fact]
    public async Task CreateUserAsync_WithoutPassword_CreatesUserWithoutPassword()
    {
        await SeedRoleAsync("user");
        _userManager.CreateAsync(Arg.Any<WallowUser>())
            .Returns(IdentityResult.Success);

        Guid result = await _sut.CreateUserAsync("john@test.com", "John", "Doe");

        result.Should().NotBeEmpty();
        await _userManager.Received(1).CreateAsync(Arg.Any<WallowUser>());
    }

    [Fact]
    public async Task CreateUserAsync_WhenCreateFails_ThrowsInvalidOperationException()
    {
        _userManager.CreateAsync(Arg.Any<WallowUser>(), Arg.Any<string>())
            .Returns(IdentityResult.Failed(new IdentityError { Description = "Duplicate email" }));

        Func<Task> act = () => _sut.CreateUserAsync("john@test.com", "John", "Doe", "Password123!");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Duplicate email*");
    }

    [Fact]
    public async Task GetUserByIdAsync_WhenUserExists_ReturnsUserDto()
    {
        Guid userId = Guid.NewGuid();
        WallowUser user = WallowUser.Create(
            "John", "Doe", "john@test.com", _timeProvider);
        _userManager.FindByIdAsync(userId.ToString()).Returns(user);

        Guid adminRoleId = await SeedRoleAsync("admin");
        await PersistMembershipAsync(user.Id, _organizationId, adminRoleId);

        UserDto? result = await _sut.GetUserByIdAsync(userId);

        result.Should().NotBeNull();
        result!.Email.Should().Be("john@test.com");
        result.FirstName.Should().Be("John");
        result.LastName.Should().Be("Doe");
        result.Roles.Should().Contain("admin");
    }

    [Fact]
    public async Task GetUserByIdAsync_DoesNotReportRolesGrantedByAnotherOrganization()
    {
        Guid userId = Guid.NewGuid();
        WallowUser user = WallowUser.Create("John", "Doe", "john@test.com", _timeProvider);
        _userManager.FindByIdAsync(userId.ToString()).Returns(user);

        Guid adminRoleId = await SeedRoleAsync("admin");
        await PersistMembershipAsync(user.Id, Guid.NewGuid(), adminRoleId);

        UserDto? result = await _sut.GetUserByIdAsync(userId);

        // A role is granted BY an organization. Showing it while administering a different one
        // claims an authority this user does not hold here.
        result.Should().NotBeNull();
        result!.Roles.Should().BeEmpty();
    }

    [Fact]
    public async Task GetUserByIdAsync_DoesNotReportRolesFromAMembershipThatIsNotActive()
    {
        Guid userId = Guid.NewGuid();
        WallowUser user = WallowUser.Create("John", "Doe", "john@test.com", _timeProvider);
        _userManager.FindByIdAsync(userId.ToString()).Returns(user);

        Guid adminRoleId = await SeedRoleAsync("admin");
        Membership membership = await PersistMembershipAsync(user.Id, _organizationId, adminRoleId);
        membership.Suspend(Guid.NewGuid(), _timeProvider);
        await _dbContext.SaveChangesAsync();

        UserDto? result = await _sut.GetUserByIdAsync(userId);

        // Same rule the authorization resolver applies: only an Active membership resolves roles.
        result.Should().NotBeNull();
        result!.Roles.Should().BeEmpty();
    }

    [Fact]
    public async Task GetUserByIdAsync_WhenUserNotFound_ReturnsNull()
    {
        _userManager.FindByIdAsync(Arg.Any<string>()).Returns((WallowUser?)null);

        UserDto? result = await _sut.GetUserByIdAsync(Guid.NewGuid());

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetUserByEmailAsync_WhenUserExists_ReturnsUserDto()
    {
        WallowUser user = WallowUser.Create(
            "Jane", "Doe", "jane@test.com", _timeProvider);
        _userManager.FindByEmailAsync("jane@test.com").Returns(user);

        Guid userRoleId = await SeedRoleAsync("user");
        await PersistMembershipAsync(user.Id, _organizationId, userRoleId);

        UserDto? result = await _sut.GetUserByEmailAsync("jane@test.com");

        result.Should().NotBeNull();
        result!.Email.Should().Be("jane@test.com");
        result.Roles.Should().Contain("user");
    }

    [Fact]
    public async Task GetUserByEmailAsync_WhenUserNotFound_ReturnsNull()
    {
        _userManager.FindByEmailAsync(Arg.Any<string>()).Returns((WallowUser?)null);

        UserDto? result = await _sut.GetUserByEmailAsync("nobody@test.com");

        result.Should().BeNull();
    }

    [Fact]
    public async Task DeactivateUserAsync_WhenUserExists_SetsLockout()
    {
        Guid userId = Guid.NewGuid();
        WallowUser user = WallowUser.Create(
            "John", "Doe", "john@test.com", _timeProvider);
        _userManager.FindByIdAsync(userId.ToString()).Returns(user);
        _userManager.SetLockoutEnabledAsync(user, true).Returns(IdentityResult.Success);
        _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue).Returns(IdentityResult.Success);

        await _sut.DeactivateUserAsync(userId);

        await _userManager.Received(1).SetLockoutEnabledAsync(user, true);
        await _userManager.Received(1).SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);
    }

    [Fact]
    public async Task DeactivateUserAsync_WhenUserNotFound_Throws()
    {
        _userManager.FindByIdAsync(Arg.Any<string>()).Returns((WallowUser?)null);

        Func<Task> act = () => _sut.DeactivateUserAsync(Guid.NewGuid());

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    [Fact]
    public async Task ActivateUserAsync_WhenUserExists_ClearsLockout()
    {
        Guid userId = Guid.NewGuid();
        WallowUser user = WallowUser.Create(
            "John", "Doe", "john@test.com", _timeProvider);
        _userManager.FindByIdAsync(userId.ToString()).Returns(user);
        _userManager.SetLockoutEnabledAsync(user, false).Returns(IdentityResult.Success);
        _userManager.SetLockoutEndDateAsync(user, null).Returns(IdentityResult.Success);

        await _sut.ActivateUserAsync(userId);

        await _userManager.Received(1).SetLockoutEnabledAsync(user, false);
        await _userManager.Received(1).SetLockoutEndDateAsync(user, null);
    }

    [Fact]
    public async Task AssignRoleAsync_WhenRoleExists_AssignsAndPublishesEvent()
    {
        Guid userId = Guid.NewGuid();
        WallowUser user = WallowUser.Create(
            "John", "Doe", "john@test.com", _timeProvider);
        Guid userRoleId = await SeedRoleAsync("user");
        Guid adminRoleId = await SeedRoleAsync("admin");
        _roleManager.RoleExistsAsync("admin").Returns(true);
        _userManager.FindByIdAsync(userId.ToString()).Returns(user);
        Membership membership = SeedMembership(userId, _organizationId, userRoleId);

        await _sut.AssignRoleAsync(userId, _organizationId, "admin", Guid.NewGuid());

        membership.RoleIds.Should().Contain(adminRoleId);
        await _messageBus.Received(1).PublishAsync(Arg.Is<UserRoleChangedEvent>(e =>
            e.UserId == userId && e.NewRole == "admin" && e.OldRole == "user"));
    }

    /// <summary>
    /// A user does not grant themselves a role - an admin does. Stamping the membership with the
    /// subject would name the person who gained the access as the person who approved it.
    /// </summary>
    [Fact]
    public async Task AssignRoleAsync_StampsTheMembershipWithTheActorNotTheSubject()
    {
        Guid userId = Guid.NewGuid();
        Guid actorId = Guid.NewGuid();
        WallowUser user = WallowUser.Create("John", "Doe", "john@test.com", _timeProvider);
        Guid userRoleId = await SeedRoleAsync("user");
        await SeedRoleAsync("admin");
        _roleManager.RoleExistsAsync("admin").Returns(true);
        _userManager.FindByIdAsync(userId.ToString()).Returns(user);
        Membership membership = SeedMembership(userId, _organizationId, userRoleId);

        await _sut.AssignRoleAsync(userId, _organizationId, "admin", actorId);

        membership.UpdatedBy.Should().Be(actorId);
    }

    /// <inheritdoc cref="AssignRoleAsync_StampsTheMembershipWithTheActorNotTheSubject"/>
    [Fact]
    public async Task RemoveRoleAsync_StampsTheMembershipWithTheActorNotTheSubject()
    {
        Guid userId = Guid.NewGuid();
        Guid actorId = Guid.NewGuid();
        WallowUser user = WallowUser.Create("John", "Doe", "john@test.com", _timeProvider);
        Guid adminRoleId = await SeedRoleAsync("admin");
        _roleManager.RoleExistsAsync("admin").Returns(true);
        _userManager.FindByIdAsync(userId.ToString()).Returns(user);
        Membership membership = SeedMembership(userId, _organizationId, adminRoleId);

        await _sut.RemoveRoleAsync(userId, _organizationId, "admin", actorId);

        membership.UpdatedBy.Should().Be(actorId);
    }

    [Fact]
    public async Task AssignRoleAsync_WhenRoleNotFound_Throws()
    {
        _roleManager.RoleExistsAsync("nonexistent").Returns(false);

        Func<Task> act = () => _sut.AssignRoleAsync(Guid.NewGuid(), _organizationId, "nonexistent", Guid.NewGuid());

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    [Fact]
    public async Task AssignRoleAsync_WhenTheUserIsNotAMemberOfTheOrganization_Throws()
    {
        Guid userId = Guid.NewGuid();
        WallowUser user = WallowUser.Create(
            "John", "Doe", "john@test.com", _timeProvider);
        await SeedRoleAsync("admin");
        _roleManager.RoleExistsAsync("admin").Returns(true);
        _userManager.FindByIdAsync(userId.ToString()).Returns(user);

        // A grant may not double as an enrollment.
        Func<Task> act = () => _sut.AssignRoleAsync(userId, _organizationId, "admin", Guid.NewGuid());

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not a member*");
    }

    [Fact]
    public async Task RemoveRoleAsync_WhenSuccessful_RemovesAndPublishesEvent()
    {
        Guid userId = Guid.NewGuid();
        WallowUser user = WallowUser.Create(
            "John", "Doe", "john@test.com", _timeProvider);
        Guid userRoleId = await SeedRoleAsync("user");
        Guid adminRoleId = await SeedRoleAsync("admin");
        _userManager.FindByIdAsync(userId.ToString()).Returns(user);
        Membership membership = SeedMembership(userId, _organizationId, userRoleId);
        membership.AssignRole(adminRoleId, userId, _timeProvider);

        await _sut.RemoveRoleAsync(userId, _organizationId, "admin", Guid.NewGuid());

        membership.RoleIds.Should().NotContain(adminRoleId);
        await _messageBus.Received(1).PublishAsync(Arg.Is<UserRoleChangedEvent>(e =>
            e.OldRole == "admin" && e.NewRole == "user"));
    }

    [Fact]
    public async Task RemoveRoleAsync_WhenUserNotFound_Throws()
    {
        _userManager.FindByIdAsync(Arg.Any<string>()).Returns((WallowUser?)null);

        Func<Task> act = () => _sut.RemoveRoleAsync(Guid.NewGuid(), _organizationId, "admin", Guid.NewGuid());

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    [Fact]
    public async Task GetUserRolesAsync_WhenUserExists_ReturnsRoles()
    {
        Guid userId = Guid.NewGuid();
        Guid userRoleId = await SeedRoleAsync("user");
        Guid adminRoleId = await SeedRoleAsync("admin");
        Membership membership = SeedMembership(userId, _organizationId, userRoleId);
        membership.AssignRole(adminRoleId, userId, _timeProvider);

        IReadOnlyList<string> result = await _sut.GetUserRolesAsync(userId, _organizationId);

        result.Should().HaveCount(2);
        result.Should().Contain("admin");
        result.Should().Contain("user");
    }

    [Fact]
    public async Task GetUserRolesAsync_WithoutAMembership_ReturnsEmpty()
    {
        IReadOnlyList<string> result = await _sut.GetUserRolesAsync(Guid.NewGuid(), _organizationId);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteUserAsync_WhenUserExists_DeletesUser()
    {
        Guid userId = Guid.NewGuid();
        WallowUser user = WallowUser.Create(
            "John", "Doe", "john@test.com", _timeProvider);
        _userManager.FindByIdAsync(userId.ToString()).Returns(user);
        _userManager.DeleteAsync(user).Returns(IdentityResult.Success);

        await _sut.DeleteUserAsync(userId);

        await _userManager.Received(1).DeleteAsync(user);
    }

    [Fact]
    public async Task DeleteUserAsync_WhenUserNotFound_Throws()
    {
        _userManager.FindByIdAsync(Arg.Any<string>()).Returns((WallowUser?)null);

        Func<Task> act = () => _sut.DeleteUserAsync(Guid.NewGuid());

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    [Fact]
    public async Task DeleteUserAsync_WhenDeleteFails_Throws()
    {
        Guid userId = Guid.NewGuid();
        WallowUser user = WallowUser.Create(
            "John", "Doe", "john@test.com", _timeProvider);
        _userManager.FindByIdAsync(userId.ToString()).Returns(user);
        _userManager.DeleteAsync(user).Returns(
            IdentityResult.Failed(new IdentityError { Description = "Cannot delete" }));

        Func<Task> act = () => _sut.DeleteUserAsync(userId);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Cannot delete*");
    }
}
