using System.Security.Claims;
using Foundry.Identity.Api.Contracts.Requests;
using Foundry.Identity.Api.Contracts.Responses;
using Foundry.Identity.Api.Controllers;
using Foundry.Identity.Application.DTOs;
using Foundry.Identity.Application.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Foundry.Identity.Tests.Api.Controllers;

public class UsersControllerTests
{
    private static readonly string[] _userRole = ["user"];
    private static readonly string[] _adminRole = ["admin"];
    private readonly IKeycloakAdminService _keycloakAdmin;
    private readonly UsersController _controller;
    private readonly Guid _userId = Guid.NewGuid();

    public UsersControllerTests()
    {
        _keycloakAdmin = Substitute.For<IKeycloakAdminService>();
        _controller = new UsersController(_keycloakAdmin);

        ClaimsPrincipal user = new(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, _userId.ToString()),
            new Claim(ClaimTypes.Email, "test@example.com"),
            new Claim(ClaimTypes.GivenName, "Test"),
            new Claim(ClaimTypes.Surname, "User"),
            new Claim(ClaimTypes.Role, "admin"),
            new Claim(ClaimTypes.Role, "user"),
            new Claim("permission", "users.read"),
            new Claim("permission", "users.write")
        }, "TestAuth"));
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user }
        };
    }

    #region GetUsers

    [Fact]
    public async Task GetUsers_ReturnsOkWithUserList()
    {
        List<UserDto> users =
        [
            new UserDto(Guid.NewGuid(), "a@test.com", "Alice", "Smith", true, _userRole),
            new UserDto(Guid.NewGuid(), "b@test.com", "Bob", "Jones", true, _adminRole)
        ];
        _keycloakAdmin.GetUsersAsync(null, 0, 20, Arg.Any<CancellationToken>())
            .Returns(users);

        ActionResult<IReadOnlyList<UserDto>> result = await _controller.GetUsers(null, 0, 20, CancellationToken.None);

        OkObjectResult ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        IReadOnlyList<UserDto> response = ok.Value.Should().BeAssignableTo<IReadOnlyList<UserDto>>().Subject;
        response.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetUsers_WithSearchFilter_PassesSearchToService()
    {
        _keycloakAdmin.GetUsersAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<UserDto>());

        await _controller.GetUsers("alice", 5, 10, CancellationToken.None);

        await _keycloakAdmin.Received(1).GetUsersAsync("alice", 5, 10, Arg.Any<CancellationToken>());
    }

    #endregion

    #region GetUserById

    [Fact]
    public async Task GetUserById_WhenFound_ReturnsOkWithUser()
    {
        Guid userId = Guid.NewGuid();
        UserDto user = new(userId, "a@test.com", "Alice", "Smith", true, _userRole);
        _keycloakAdmin.GetUserByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(user);

        ActionResult<UserDto> result = await _controller.GetUserById(userId, CancellationToken.None);

        OkObjectResult ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        UserDto response = ok.Value.Should().BeOfType<UserDto>().Subject;
        response.Id.Should().Be(userId);
    }

    [Fact]
    public async Task GetUserById_WhenNotFound_ReturnsNotFound()
    {
        Guid userId = Guid.NewGuid();
        _keycloakAdmin.GetUserByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns((UserDto?)null);

        ActionResult<UserDto> result = await _controller.GetUserById(userId, CancellationToken.None);

        result.Result.Should().BeOfType<NotFoundResult>();
    }

    #endregion

    #region GetCurrentUser

    [Fact]
    public void GetCurrentUser_ReturnsCurrentUserFromClaims()
    {
        ActionResult<CurrentUserResponse> result = _controller.GetCurrentUser();

        OkObjectResult ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        CurrentUserResponse response = ok.Value.Should().BeOfType<CurrentUserResponse>().Subject;
        response.Id.Should().Be(_userId);
        response.Email.Should().Be("test@example.com");
        response.FirstName.Should().Be("Test");
        response.LastName.Should().Be("User");
        response.Roles.Should().Contain("admin");
        response.Roles.Should().Contain("user");
        response.Permissions.Should().Contain("users.read");
        response.Permissions.Should().Contain("users.write");
    }

    [Fact]
    public void GetCurrentUser_WithMissingOptionalClaims_UsesEmptyDefaults()
    {
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, _userId.ToString())
                }, "TestAuth"))
            }
        };

        ActionResult<CurrentUserResponse> result = _controller.GetCurrentUser();

        OkObjectResult ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        CurrentUserResponse response = ok.Value.Should().BeOfType<CurrentUserResponse>().Subject;
        response.Email.Should().Be(string.Empty);
        response.FirstName.Should().Be(string.Empty);
        response.LastName.Should().Be(string.Empty);
        response.Roles.Should().BeEmpty();
        response.Permissions.Should().BeEmpty();
    }

    #endregion

    #region CreateUser

    [Fact]
    public async Task CreateUser_WithValidRequest_ReturnsCreatedAtAction()
    {
        Guid newUserId = Guid.NewGuid();
        CreateUserRequest request = new("new@test.com", "New", "User", "password123");
        UserDto createdUser = new(newUserId, "new@test.com", "New", "User", true, Array.Empty<string>());
        _keycloakAdmin.CreateUserAsync("new@test.com", "New", "User", "password123", Arg.Any<CancellationToken>())
            .Returns(newUserId);
        _keycloakAdmin.GetUserByIdAsync(newUserId, Arg.Any<CancellationToken>())
            .Returns(createdUser);

        ActionResult result = await _controller.CreateUser(request, CancellationToken.None);

        CreatedAtActionResult created = result.Should().BeOfType<CreatedAtActionResult>().Subject;
        created.ActionName.Should().Be(nameof(UsersController.GetUserById));
        created.RouteValues!["id"].Should().Be(newUserId);
        UserDto responseUser = created.Value.Should().BeOfType<UserDto>().Subject;
        responseUser.Email.Should().Be("new@test.com");
    }

    [Fact]
    public async Task CreateUser_PassesCorrectFieldsToService()
    {
        Guid newUserId = Guid.NewGuid();
        CreateUserRequest request = new("test@test.com", "First", "Last", "pass");
        _keycloakAdmin.CreateUserAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(newUserId);
        _keycloakAdmin.GetUserByIdAsync(newUserId, Arg.Any<CancellationToken>())
            .Returns(new UserDto(newUserId, "test@test.com", "First", "Last", true, Array.Empty<string>()));

        await _controller.CreateUser(request, CancellationToken.None);

        await _keycloakAdmin.Received(1).CreateUserAsync("test@test.com", "First", "Last", "pass", Arg.Any<CancellationToken>());
    }

    #endregion

    #region DeactivateUser

    [Fact]
    public async Task DeactivateUser_ReturnsNoContent()
    {
        Guid userId = Guid.NewGuid();

        ActionResult result = await _controller.DeactivateUser(userId, CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
        await _keycloakAdmin.Received(1).DeactivateUserAsync(userId, Arg.Any<CancellationToken>());
    }

    #endregion

    #region ActivateUser

    [Fact]
    public async Task ActivateUser_ReturnsNoContent()
    {
        Guid userId = Guid.NewGuid();

        ActionResult result = await _controller.ActivateUser(userId, CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
        await _keycloakAdmin.Received(1).ActivateUserAsync(userId, Arg.Any<CancellationToken>());
    }

    #endregion

    #region AssignRole

    [Fact]
    public async Task AssignRole_ReturnsNoContent()
    {
        Guid userId = Guid.NewGuid();
        AssignRoleRequest request = new("admin");

        ActionResult result = await _controller.AssignRole(userId, request, CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
        await _keycloakAdmin.Received(1).AssignRoleAsync(userId, "admin", Arg.Any<CancellationToken>());
    }

    #endregion

    #region RemoveRole

    [Fact]
    public async Task RemoveRole_ReturnsNoContent()
    {
        Guid userId = Guid.NewGuid();

        ActionResult result = await _controller.RemoveRole(userId, "admin", CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
        await _keycloakAdmin.Received(1).RemoveRoleAsync(userId, "admin", Arg.Any<CancellationToken>());
    }

    #endregion
}
