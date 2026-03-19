using Foundry.Identity.Application.DTOs;
using Foundry.Identity.Application.Interfaces;
using Foundry.Identity.Infrastructure.Services;
using Foundry.Shared.Contracts.Identity;

namespace Foundry.Identity.Tests.Infrastructure;

public class UserServiceTests
{
    private static readonly string[] _userRole = ["user"];
    private static readonly string[] _adminRole = ["admin"];
    private readonly IUserManagementService _keycloakAdmin = Substitute.For<IUserManagementService>();
    private readonly UserService _service;

    public UserServiceTests()
    {
        _service = new UserService(_keycloakAdmin);
    }

    [Fact]
    public async Task GetUserByIdAsync_WhenUserExists_ReturnsUserInfo()
    {
        Guid userId = Guid.NewGuid();
        UserDto userDto = new(userId, "test@example.com", "John", "Doe", true, _userRole);
        _keycloakAdmin.GetUserByIdAsync(userId, Arg.Any<CancellationToken>()).Returns(userDto);

        UserInfo? result = await _service.GetUserByIdAsync(userId);

        result.Should().NotBeNull();
        result.Id.Should().Be(userId);
        result.Email.Should().Be("test@example.com");
        result.FirstName.Should().Be("John");
        result.LastName.Should().Be("Doe");
        result.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task GetUserByIdAsync_WhenUserNotFound_ReturnsNull()
    {
        Guid userId = Guid.NewGuid();
        _keycloakAdmin.GetUserByIdAsync(userId, Arg.Any<CancellationToken>()).Returns((UserDto?)null);

        UserInfo? result = await _service.GetUserByIdAsync(userId);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetUserByEmailAsync_WhenUserExists_ReturnsUserInfo()
    {
        Guid userId = Guid.NewGuid();
        string email = "jane@example.com";
        UserDto userDto = new(userId, email, "Jane", "Smith", false, _adminRole);
        _keycloakAdmin.GetUserByEmailAsync(email, Arg.Any<CancellationToken>()).Returns(userDto);

        UserInfo? result = await _service.GetUserByEmailAsync(email);

        result.Should().NotBeNull();
        result.Id.Should().Be(userId);
        result.Email.Should().Be(email);
        result.FirstName.Should().Be("Jane");
        result.LastName.Should().Be("Smith");
        result.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task GetUserByEmailAsync_WhenUserNotFound_ReturnsNull()
    {
        _keycloakAdmin.GetUserByEmailAsync("unknown@example.com", Arg.Any<CancellationToken>()).Returns((UserDto?)null);

        UserInfo? result = await _service.GetUserByEmailAsync("unknown@example.com");

        result.Should().BeNull();
    }
}
