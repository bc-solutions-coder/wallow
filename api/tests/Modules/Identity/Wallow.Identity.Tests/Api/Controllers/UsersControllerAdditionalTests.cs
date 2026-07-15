using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Wallow.Identity.Api.Contracts.Requests;
using Wallow.Identity.Api.Contracts.Responses;
using Wallow.Identity.Api.Controllers;
using Wallow.Identity.Application.DTOs;
using Wallow.Identity.Application.Interfaces;
using Wallow.Shared.Contracts.Identity;
using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.MultiTenancy;
using Wallow.Shared.Kernel.Pagination;

namespace Wallow.Identity.Tests.Api.Controllers;

public class UsersControllerAdditionalTests
{
    private static readonly string[] _adminUserRoles = ["admin", "user"];
    private static readonly string[] _expectedRoles = ["admin", "manager"];
    private static readonly string[] _expectedPermissions = ["users.read", "users.write", "roles.update"];
    private static readonly Guid _tenantGuid = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly IUserManagementService _userManagement;
    private readonly IOrganizationService _organizationService;
    private readonly IUserQueryService _userQueryService;
    private readonly UsersController _controller;

    public UsersControllerAdditionalTests()
    {
        _userManagement = Substitute.For<IUserManagementService>();
        _organizationService = Substitute.For<IOrganizationService>();
        _userQueryService = Substitute.For<IUserQueryService>();
        ITenantContext tenantContext = Substitute.For<ITenantContext>();
        tenantContext.TenantId.Returns(new TenantId(_tenantGuid));
        _controller = new UsersController(_userManagement, _organizationService, _userQueryService, tenantContext);

        ClaimsPrincipal user = new(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, _userId.ToString()),
            new Claim(ClaimTypes.Email, "current@example.com"),
            new Claim(ClaimTypes.GivenName, "Current"),
            new Claim(ClaimTypes.Surname, "User"),
            new Claim(ClaimTypes.Role, "admin"),
            new Claim(ClaimTypes.Role, "manager"),
            new Claim("permission", "users.read"),
            new Claim("permission", "users.write"),
            new Claim("permission", "roles.update")
        }, "TestAuth"));
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user }
        };
    }

    #region GetUserById

    [Fact]
    public async Task GetUserById_WhenUserNotInTenant_ReturnsNotFound()
    {
        Guid userId = Guid.NewGuid();
        UserDto user = new(userId, "a@test.com", "Alice", "Smith", true, []);
        _userManagement.GetUserByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(user);
        _organizationService.GetUserOrganizationsAsync(userId, Arg.Any<CancellationToken>())
            .Returns(new List<OrganizationDto> { new(Guid.NewGuid(), "Other Org", null, 1) });

        ActionResult<UserDto> result = await _controller.GetUserById(userId, CancellationToken.None);

        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetUserById_WhenUserHasNoOrganizations_ReturnsNotFound()
    {
        Guid userId = Guid.NewGuid();
        UserDto user = new(userId, "a@test.com", "Alice", "Smith", true, []);
        _userManagement.GetUserByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(user);
        _organizationService.GetUserOrganizationsAsync(userId, Arg.Any<CancellationToken>())
            .Returns(new List<OrganizationDto>());

        ActionResult<UserDto> result = await _controller.GetUserById(userId, CancellationToken.None);

        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetUserById_WhenUserExistsAndBelongsToTenant_ReturnsOkWithAllFields()
    {
        Guid userId = Guid.NewGuid();
        UserDto user = new(userId, "alice@test.com", "Alice", "Smith", true, _adminUserRoles);
        _userManagement.GetUserByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(user);
        _organizationService.GetUserOrganizationsAsync(userId, Arg.Any<CancellationToken>())
            .Returns(new List<OrganizationDto> { new(_tenantGuid, "Test Org", null, 5) });

        ActionResult<UserDto> result = await _controller.GetUserById(userId, CancellationToken.None);

        OkObjectResult ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        UserDto dto = ok.Value.Should().BeOfType<UserDto>().Subject;
        dto.Id.Should().Be(userId);
        dto.Email.Should().Be("alice@test.com");
        dto.FirstName.Should().Be("Alice");
        dto.LastName.Should().Be("Smith");
        dto.Enabled.Should().BeTrue();
        dto.Roles.Should().BeEquivalentTo(_adminUserRoles);
    }

    #endregion

    #region GetCurrentUser

    [Fact]
    public void GetCurrentUser_ReturnsExactRoleAndPermissionCounts()
    {
        ActionResult<CurrentUserResponse> result = _controller.GetCurrentUser();

        OkObjectResult ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        CurrentUserResponse response = ok.Value.Should().BeOfType<CurrentUserResponse>().Subject;
        response.Id.Should().Be(_userId);
        response.Email.Should().Be("current@example.com");
        response.FirstName.Should().Be("Current");
        response.LastName.Should().Be("User");
        response.Roles.Should().HaveCount(2);
        response.Roles.Should().BeEquivalentTo(_expectedRoles);
        response.Permissions.Should().HaveCount(3);
        response.Permissions.Should().BeEquivalentTo(_expectedPermissions);
    }

    #endregion

    #region DeactivateUser

    [Fact]
    public async Task DeactivateUser_WhenUserNotInTenant_ReturnsNotFound()
    {
        Guid userId = Guid.NewGuid();
        _organizationService.GetUserOrganizationsAsync(userId, Arg.Any<CancellationToken>())
            .Returns(new List<OrganizationDto> { new(Guid.NewGuid(), "Other Org", null, 1) });

        ActionResult result = await _controller.DeactivateUser(userId, CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
        await _userManagement.DidNotReceive().DeactivateUserAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeactivateUser_WhenUserInTenant_CallsServiceAndReturnsNoContent()
    {
        Guid userId = Guid.NewGuid();
        _organizationService.GetUserOrganizationsAsync(userId, Arg.Any<CancellationToken>())
            .Returns(new List<OrganizationDto> { new(_tenantGuid, "Test Org", null, 3) });

        ActionResult result = await _controller.DeactivateUser(userId, CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
        await _userManagement.Received(1).DeactivateUserAsync(userId, Arg.Any<CancellationToken>());
    }

    #endregion

    #region ActivateUser

    [Fact]
    public async Task ActivateUser_WhenUserNotInTenant_ReturnsNotFound()
    {
        Guid userId = Guid.NewGuid();
        _organizationService.GetUserOrganizationsAsync(userId, Arg.Any<CancellationToken>())
            .Returns(new List<OrganizationDto>());

        ActionResult result = await _controller.ActivateUser(userId, CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
        await _userManagement.DidNotReceive().ActivateUserAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ActivateUser_WhenUserInTenant_CallsServiceAndReturnsNoContent()
    {
        Guid userId = Guid.NewGuid();
        _organizationService.GetUserOrganizationsAsync(userId, Arg.Any<CancellationToken>())
            .Returns(new List<OrganizationDto> { new(_tenantGuid, "Test Org", null, 2) });

        ActionResult result = await _controller.ActivateUser(userId, CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
        await _userManagement.Received(1).ActivateUserAsync(userId, Arg.Any<CancellationToken>());
    }

    #endregion

    #region AssignRole

    [Fact]
    public async Task AssignRole_WhenUserNotInTenant_ReturnsNotFound()
    {
        Guid userId = Guid.NewGuid();
        AssignRoleRequest request = new("admin");
        _organizationService.GetUserOrganizationsAsync(userId, Arg.Any<CancellationToken>())
            .Returns(new List<OrganizationDto>());

        ActionResult result = await _controller.AssignRole(userId, request, CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
        await _userManagement.DidNotReceive().AssignRoleAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AssignRole_WhenUserInTenant_PassesCorrectRoleNameToService()
    {
        Guid userId = Guid.NewGuid();
        AssignRoleRequest request = new("manager");
        _organizationService.GetUserOrganizationsAsync(userId, Arg.Any<CancellationToken>())
            .Returns(new List<OrganizationDto> { new(_tenantGuid, "Test Org", null, 1) });

        ActionResult result = await _controller.AssignRole(userId, request, CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
        await _userManagement.Received(1).AssignRoleAsync(userId, "manager", Arg.Any<CancellationToken>());
    }

    #endregion

    #region RemoveRole

    [Fact]
    public async Task RemoveRole_WhenUserNotInTenant_ReturnsNotFound()
    {
        Guid userId = Guid.NewGuid();
        _organizationService.GetUserOrganizationsAsync(userId, Arg.Any<CancellationToken>())
            .Returns(new List<OrganizationDto>());

        ActionResult result = await _controller.RemoveRole(userId, "admin", CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
        await _userManagement.DidNotReceive().RemoveRoleAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RemoveRole_WhenUserInTenant_PassesCorrectRoleNameToService()
    {
        Guid userId = Guid.NewGuid();
        _organizationService.GetUserOrganizationsAsync(userId, Arg.Any<CancellationToken>())
            .Returns(new List<OrganizationDto> { new(_tenantGuid, "Test Org", null, 1) });

        ActionResult result = await _controller.RemoveRole(userId, "editor", CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
        await _userManagement.Received(1).RemoveRoleAsync(userId, "editor", Arg.Any<CancellationToken>());
    }

    #endregion

    #region GetUsers

    [Fact]
    public async Task GetUsers_ReturnsPaginationMetadata()
    {
        List<UserSearchItem> items =
        [
            new(Guid.NewGuid(), "a@test.com", "Alice", "Smith", true, ["user"])
        ];
        _userQueryService.SearchUsersAsync(_tenantGuid, null, 5, 10, Arg.Any<CancellationToken>())
            .Returns(new UserSearchPageResult(items, 25, 5, 10));

        ActionResult<PagedResult<UserDto>> result = await _controller.GetUsers(null, 5, 10, CancellationToken.None);

        OkObjectResult ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        PagedResult<UserDto> page = ok.Value.Should().BeOfType<PagedResult<UserDto>>().Subject;
        page.TotalCount.Should().Be(25);
        page.Page.Should().Be(5);
        page.PageSize.Should().Be(10);
        page.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetUsers_MapsAllUserFieldsCorrectly()
    {
        Guid userId = Guid.NewGuid();
        List<UserSearchItem> items =
        [
            new(userId, "mapped@test.com", "Mapped", "User", false, _adminUserRoles)
        ];
        _userQueryService.SearchUsersAsync(_tenantGuid, null, 0, 20, Arg.Any<CancellationToken>())
            .Returns(new UserSearchPageResult(items, 1, 0, 20));

        ActionResult<PagedResult<UserDto>> result = await _controller.GetUsers(null, 0, 20, CancellationToken.None);

        OkObjectResult ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        PagedResult<UserDto> page = ok.Value.Should().BeOfType<PagedResult<UserDto>>().Subject;
        UserDto dto = page.Items[0];
        dto.Id.Should().Be(userId);
        dto.Email.Should().Be("mapped@test.com");
        dto.FirstName.Should().Be("Mapped");
        dto.LastName.Should().Be("User");
        dto.Enabled.Should().BeFalse();
        dto.Roles.Should().BeEquivalentTo(_adminUserRoles);
    }

    [Fact]
    public async Task GetUsers_WithMaxZero_StillReturnsPage()
    {
        _userQueryService.SearchUsersAsync(_tenantGuid, null, 0, 0, Arg.Any<CancellationToken>())
            .Returns(new UserSearchPageResult([], 0, 0, 0));

        ActionResult<PagedResult<UserDto>> result =
            await _controller.GetUsers(null, 0, 0, CancellationToken.None);

        result.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetUsers_SearchByLastName_FiltersResults()
    {
        List<UserSearchItem> items =
        [
            new(Guid.NewGuid(), "bob@test.com", "Bob", "Jones", true, [])
        ];
        _userQueryService.SearchUsersAsync(_tenantGuid, "Jones", 0, 10, Arg.Any<CancellationToken>())
            .Returns(new UserSearchPageResult(items, 1, 0, 10));

        ActionResult<PagedResult<UserDto>> result =
            await _controller.GetUsers("Jones", 0, 10, CancellationToken.None);

        OkObjectResult ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        PagedResult<UserDto> page = ok.Value.Should().BeOfType<PagedResult<UserDto>>().Subject;
        page.Items.Should().HaveCount(1);
        page.Items[0].LastName.Should().Be("Jones");
    }

    #endregion

    #region CreateUser

    [Fact]
    public async Task CreateUser_AddsMemberToCorrectTenant()
    {
        Guid newUserId = Guid.NewGuid();
        CreateUserRequest request = new("new@test.com", "New", "User", "password123");
        _userManagement.CreateUserAsync("new@test.com", "New", "User", "password123", Arg.Any<CancellationToken>())
            .Returns(newUserId);
        _userManagement.GetUserByIdAsync(newUserId, Arg.Any<CancellationToken>())
            .Returns(new UserDto(newUserId, "new@test.com", "New", "User", true, []));

        await _controller.CreateUser(request, CancellationToken.None);

        await _organizationService.Received(1).AddMemberAsync(_tenantGuid, newUserId, Arg.Any<CancellationToken>());
    }

    #endregion
}
