using Foundry.Identity.Application.DTOs;
using Foundry.Identity.Domain.Entities;
using Foundry.Identity.Infrastructure.Services;
using Foundry.Shared.Contracts.Identity.Events;
using Foundry.Shared.Kernel.MultiTenancy;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.TimeProvider.Testing;
using Wolverine;

namespace Foundry.Identity.Tests.Infrastructure;

public class UserManagementServiceTests
{
    private readonly UserManager<FoundryUser> _userManager;
    private readonly RoleManager<FoundryRole> _roleManager;
    private readonly IMessageBus _messageBus;
    private readonly ITenantContext _tenantContext;
    private readonly FakeTimeProvider _timeProvider;
    private readonly UserManagementService _sut;

    public UserManagementServiceTests()
    {
        IUserStore<FoundryUser> userStore = Substitute.For<IUserStore<FoundryUser>>();
        _userManager = Substitute.For<UserManager<FoundryUser>>(
            userStore, null, null, null, null, null, null, null, null);

        IRoleStore<FoundryRole> roleStore = Substitute.For<IRoleStore<FoundryRole>>();
        _roleManager = Substitute.For<RoleManager<FoundryRole>>(
            roleStore, null, null, null, null);

        _messageBus = Substitute.For<IMessageBus>();
        _tenantContext = Substitute.For<ITenantContext>();
        _tenantContext.TenantId.Returns(new TenantId(Guid.NewGuid()));
        _timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);

        _sut = new UserManagementService(
            _userManager,
            _roleManager,
            _messageBus,
            _tenantContext,
            _timeProvider,
            NullLoggerFactory.Instance.CreateLogger<UserManagementService>());
    }

    [Fact]
    public async Task CreateUserAsync_WithPassword_CreatesUserAndPublishesEvent()
    {
        _userManager.CreateAsync(Arg.Any<FoundryUser>(), Arg.Any<string>())
            .Returns(IdentityResult.Success);
        _roleManager.RoleExistsAsync("user").Returns(true);
        _userManager.FindByIdAsync(Arg.Any<string>()).Returns(ci =>
        {
            FoundryUser user = FoundryUser.Create(
                _tenantContext.TenantId.Value, "John", "Doe", "john@test.com", _timeProvider);
            return user;
        });
        _userManager.AddToRoleAsync(Arg.Any<FoundryUser>(), "user")
            .Returns(IdentityResult.Success);
        _userManager.GetRolesAsync(Arg.Any<FoundryUser>())
            .Returns(new List<string>());

        Guid result = await _sut.CreateUserAsync("john@test.com", "John", "Doe", "Password123!");

        result.Should().NotBeEmpty();
        await _userManager.Received(1).CreateAsync(Arg.Any<FoundryUser>(), "Password123!");
        await _messageBus.Received(1).PublishAsync(Arg.Any<UserRegisteredEvent>());
    }

    [Fact]
    public async Task CreateUserAsync_WithoutPassword_CreatesUserWithoutPassword()
    {
        _userManager.CreateAsync(Arg.Any<FoundryUser>())
            .Returns(IdentityResult.Success);
        _roleManager.RoleExistsAsync("user").Returns(true);
        _userManager.FindByIdAsync(Arg.Any<string>()).Returns(ci =>
        {
            FoundryUser user = FoundryUser.Create(
                _tenantContext.TenantId.Value, "John", "Doe", "john@test.com", _timeProvider);
            return user;
        });
        _userManager.AddToRoleAsync(Arg.Any<FoundryUser>(), "user")
            .Returns(IdentityResult.Success);
        _userManager.GetRolesAsync(Arg.Any<FoundryUser>())
            .Returns(new List<string>());

        Guid result = await _sut.CreateUserAsync("john@test.com", "John", "Doe");

        result.Should().NotBeEmpty();
        await _userManager.Received(1).CreateAsync(Arg.Any<FoundryUser>());
    }

    [Fact]
    public async Task CreateUserAsync_WhenCreateFails_ThrowsInvalidOperationException()
    {
        _userManager.CreateAsync(Arg.Any<FoundryUser>(), Arg.Any<string>())
            .Returns(IdentityResult.Failed(new IdentityError { Description = "Duplicate email" }));

        Func<Task> act = () => _sut.CreateUserAsync("john@test.com", "John", "Doe", "Password123!");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Duplicate email*");
    }

    [Fact]
    public async Task GetUserByIdAsync_WhenUserExists_ReturnsUserDto()
    {
        Guid userId = Guid.NewGuid();
        FoundryUser user = FoundryUser.Create(
            _tenantContext.TenantId.Value, "John", "Doe", "john@test.com", _timeProvider);
        _userManager.FindByIdAsync(userId.ToString()).Returns(user);
        _userManager.GetRolesAsync(user).Returns(new List<string> { "admin" });

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
        _userManager.FindByIdAsync(Arg.Any<string>()).Returns((FoundryUser?)null);

        UserDto? result = await _sut.GetUserByIdAsync(Guid.NewGuid());

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetUserByEmailAsync_WhenUserExists_ReturnsUserDto()
    {
        FoundryUser user = FoundryUser.Create(
            _tenantContext.TenantId.Value, "Jane", "Doe", "jane@test.com", _timeProvider);
        _userManager.FindByEmailAsync("jane@test.com").Returns(user);
        _userManager.GetRolesAsync(user).Returns(new List<string> { "user" });

        UserDto? result = await _sut.GetUserByEmailAsync("jane@test.com");

        result.Should().NotBeNull();
        result!.Email.Should().Be("jane@test.com");
    }

    [Fact]
    public async Task GetUserByEmailAsync_WhenUserNotFound_ReturnsNull()
    {
        _userManager.FindByEmailAsync(Arg.Any<string>()).Returns((FoundryUser?)null);

        UserDto? result = await _sut.GetUserByEmailAsync("nobody@test.com");

        result.Should().BeNull();
    }

    [Fact]
    public async Task DeactivateUserAsync_WhenUserExists_SetsLockout()
    {
        Guid userId = Guid.NewGuid();
        FoundryUser user = FoundryUser.Create(
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
        _userManager.FindByIdAsync(Arg.Any<string>()).Returns((FoundryUser?)null);

        Func<Task> act = () => _sut.DeactivateUserAsync(Guid.NewGuid());

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    [Fact]
    public async Task ActivateUserAsync_WhenUserExists_ClearsLockout()
    {
        Guid userId = Guid.NewGuid();
        FoundryUser user = FoundryUser.Create(
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
        FoundryUser user = FoundryUser.Create(
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
        FoundryUser user = FoundryUser.Create(
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
        _userManager.FindByIdAsync(Arg.Any<string>()).Returns((FoundryUser?)null);

        Func<Task> act = () => _sut.RemoveRoleAsync(Guid.NewGuid(), "admin");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    [Fact]
    public async Task GetUserRolesAsync_WhenUserExists_ReturnsRoles()
    {
        Guid userId = Guid.NewGuid();
        FoundryUser user = FoundryUser.Create(
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
        _userManager.FindByIdAsync(Arg.Any<string>()).Returns((FoundryUser?)null);

        IReadOnlyList<string> result = await _sut.GetUserRolesAsync(Guid.NewGuid());

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteUserAsync_WhenUserExists_DeletesUser()
    {
        Guid userId = Guid.NewGuid();
        FoundryUser user = FoundryUser.Create(
            _tenantContext.TenantId.Value, "John", "Doe", "john@test.com", _timeProvider);
        _userManager.FindByIdAsync(userId.ToString()).Returns(user);
        _userManager.DeleteAsync(user).Returns(IdentityResult.Success);

        await _sut.DeleteUserAsync(userId);

        await _userManager.Received(1).DeleteAsync(user);
    }

    [Fact]
    public async Task DeleteUserAsync_WhenUserNotFound_Throws()
    {
        _userManager.FindByIdAsync(Arg.Any<string>()).Returns((FoundryUser?)null);

        Func<Task> act = () => _sut.DeleteUserAsync(Guid.NewGuid());

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    [Fact]
    public async Task DeleteUserAsync_WhenDeleteFails_Throws()
    {
        Guid userId = Guid.NewGuid();
        FoundryUser user = FoundryUser.Create(
            _tenantContext.TenantId.Value, "John", "Doe", "john@test.com", _timeProvider);
        _userManager.FindByIdAsync(userId.ToString()).Returns(user);
        _userManager.DeleteAsync(user).Returns(
            IdentityResult.Failed(new IdentityError { Description = "Cannot delete" }));

        Func<Task> act = () => _sut.DeleteUserAsync(userId);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Cannot delete*");
    }
}
