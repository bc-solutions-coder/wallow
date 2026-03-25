using Wallow.Identity.Application.DTOs;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Infrastructure.Persistence;
using Wallow.Identity.Infrastructure.Services;
using Wallow.Shared.Contracts.Identity.Events;
using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.MultiTenancy;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
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
    private readonly UserManagementService _sut;

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
        _tenantContext.TenantId.Returns(new TenantId(Guid.NewGuid()));
        _timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);

        DbContextOptions<IdentityDbContext> dbOptions = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new IdentityDbContext(dbOptions,
            Microsoft.AspNetCore.DataProtection.DataProtectionProvider.Create("test"));
        _sut = new UserManagementService(
            _userManager,
            _roleManager,
            _dbContext,
            _messageBus,
            _tenantContext,
            _timeProvider,
            NullLoggerFactory.Instance.CreateLogger<UserManagementService>());
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
        _userManager.CreateAsync(Arg.Any<WallowUser>(), Arg.Any<string>())
            .Returns(IdentityResult.Success);
        _roleManager.RoleExistsAsync("user").Returns(true);
        _userManager.FindByIdAsync(Arg.Any<string>()).Returns(ci =>
        {
            WallowUser user = WallowUser.Create(
                _tenantContext.TenantId.Value, "John", "Doe", "john@test.com", _timeProvider);
            return user;
        });
        _userManager.AddToRoleAsync(Arg.Any<WallowUser>(), "user")
            .Returns(IdentityResult.Success);
        _userManager.GetRolesAsync(Arg.Any<WallowUser>())
            .Returns(new List<string>());

        Guid result = await _sut.CreateUserAsync("john@test.com", "John", "Doe", "Password123!");

        result.Should().NotBeEmpty();
        await _userManager.Received(1).CreateAsync(Arg.Any<WallowUser>(), "Password123!");
        await _messageBus.Received(1).PublishAsync(Arg.Any<UserRegisteredEvent>());
    }

    [Fact]
    public async Task CreateUserAsync_WithoutPassword_CreatesUserWithoutPassword()
    {
        _userManager.CreateAsync(Arg.Any<WallowUser>())
            .Returns(IdentityResult.Success);
        _roleManager.RoleExistsAsync("user").Returns(true);
        _userManager.FindByIdAsync(Arg.Any<string>()).Returns(ci =>
        {
            WallowUser user = WallowUser.Create(
                _tenantContext.TenantId.Value, "John", "Doe", "john@test.com", _timeProvider);
            return user;
        });
        _userManager.AddToRoleAsync(Arg.Any<WallowUser>(), "user")
            .Returns(IdentityResult.Success);
        _userManager.GetRolesAsync(Arg.Any<WallowUser>())
            .Returns(new List<string>());

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
            _tenantContext.TenantId.Value, "John", "Doe", "john@test.com", _timeProvider);
        _userManager.FindByIdAsync(userId.ToString()).Returns(user);

        WallowRole adminRole = new() { Id = Guid.NewGuid(), Name = "admin", NormalizedName = "ADMIN", TenantId = _tenantContext.TenantId.Value };
        _dbContext.Roles.Add(adminRole);
        _dbContext.UserRoles.Add(new Microsoft.AspNetCore.Identity.IdentityUserRole<Guid> { UserId = user.Id, RoleId = adminRole.Id });
        await _dbContext.SaveChangesAsync();

        UserDto? result = await _sut.GetUserByIdAsync(userId);

        result.Should().NotBeNull();
        result!.Email.Should().Be("john@test.com");
        result.FirstName.Should().Be("John");
        result.LastName.Should().Be("Doe");
        result.Roles.Should().Contain("admin");
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
            _tenantContext.TenantId.Value, "Jane", "Doe", "jane@test.com", _timeProvider);
        _userManager.FindByEmailAsync("jane@test.com").Returns(user);

        WallowRole userRole = new() { Id = Guid.NewGuid(), Name = "user", NormalizedName = "USER", TenantId = _tenantContext.TenantId.Value };
        _dbContext.Roles.Add(userRole);
        _dbContext.UserRoles.Add(new Microsoft.AspNetCore.Identity.IdentityUserRole<Guid> { UserId = user.Id, RoleId = userRole.Id });
        await _dbContext.SaveChangesAsync();

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
            _tenantContext.TenantId.Value, "John", "Doe", "john@test.com", _timeProvider);
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
            _tenantContext.TenantId.Value, "John", "Doe", "john@test.com", _timeProvider);
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
            _tenantContext.TenantId.Value, "John", "Doe", "john@test.com", _timeProvider);
        _roleManager.RoleExistsAsync("admin").Returns(true);
        _userManager.FindByIdAsync(userId.ToString()).Returns(user);
        _userManager.GetRolesAsync(user).Returns(new List<string> { "user" });
        _userManager.AddToRoleAsync(user, "admin").Returns(IdentityResult.Success);

        await _sut.AssignRoleAsync(userId, "admin");

        await _userManager.Received(1).AddToRoleAsync(user, "admin");
        await _messageBus.Received(1).PublishAsync(Arg.Is<UserRoleChangedEvent>(e =>
            e.UserId == userId && e.NewRole == "admin" && e.OldRole == "user"));
    }

    [Fact]
    public async Task AssignRoleAsync_WhenRoleNotFound_Throws()
    {
        _roleManager.RoleExistsAsync("nonexistent").Returns(false);

        Func<Task> act = () => _sut.AssignRoleAsync(Guid.NewGuid(), "nonexistent");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    [Fact]
    public async Task RemoveRoleAsync_WhenSuccessful_RemovesAndPublishesEvent()
    {
        Guid userId = Guid.NewGuid();
        WallowUser user = WallowUser.Create(
            _tenantContext.TenantId.Value, "John", "Doe", "john@test.com", _timeProvider);
        _userManager.FindByIdAsync(userId.ToString()).Returns(user);
        _userManager.RemoveFromRoleAsync(user, "admin").Returns(IdentityResult.Success);
        _userManager.GetRolesAsync(user).Returns(new List<string> { "user" });

        await _sut.RemoveRoleAsync(userId, "admin");

        await _userManager.Received(1).RemoveFromRoleAsync(user, "admin");
        await _messageBus.Received(1).PublishAsync(Arg.Is<UserRoleChangedEvent>(e =>
            e.OldRole == "admin" && e.NewRole == "user"));
    }

    [Fact]
    public async Task RemoveRoleAsync_WhenUserNotFound_Throws()
    {
        _userManager.FindByIdAsync(Arg.Any<string>()).Returns((WallowUser?)null);

        Func<Task> act = () => _sut.RemoveRoleAsync(Guid.NewGuid(), "admin");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    [Fact]
    public async Task GetUserRolesAsync_WhenUserExists_ReturnsRoles()
    {
        Guid userId = Guid.NewGuid();
        WallowUser user = WallowUser.Create(
            _tenantContext.TenantId.Value, "John", "Doe", "john@test.com", _timeProvider);
        _userManager.FindByIdAsync(userId.ToString()).Returns(user);
        _userManager.GetRolesAsync(user).Returns(new List<string> { "admin", "user" });

        IReadOnlyList<string> result = await _sut.GetUserRolesAsync(userId);

        result.Should().HaveCount(2);
        result.Should().Contain("admin");
        result.Should().Contain("user");
    }

    [Fact]
    public async Task GetUserRolesAsync_WhenUserNotFound_ReturnsEmpty()
    {
        _userManager.FindByIdAsync(Arg.Any<string>()).Returns((WallowUser?)null);

        IReadOnlyList<string> result = await _sut.GetUserRolesAsync(Guid.NewGuid());

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteUserAsync_WhenUserExists_DeletesUser()
    {
        Guid userId = Guid.NewGuid();
        WallowUser user = WallowUser.Create(
            _tenantContext.TenantId.Value, "John", "Doe", "john@test.com", _timeProvider);
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
            _tenantContext.TenantId.Value, "John", "Doe", "john@test.com", _timeProvider);
        _userManager.FindByIdAsync(userId.ToString()).Returns(user);
        _userManager.DeleteAsync(user).Returns(
            IdentityResult.Failed(new IdentityError { Description = "Cannot delete" }));

        Func<Task> act = () => _sut.DeleteUserAsync(userId);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Cannot delete*");
    }
}
