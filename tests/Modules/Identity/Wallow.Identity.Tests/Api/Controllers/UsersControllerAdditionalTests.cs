using Wallow.Identity.Api.Contracts.Requests;
using Wallow.Identity.Api.Controllers;
using Wallow.Identity.Application.DTOs;
using Wallow.Identity.Application.Interfaces;
using Wallow.Shared.Contracts.Identity;
using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.MultiTenancy;
using Microsoft.AspNetCore.Mvc;

namespace Wallow.Identity.Tests.Api.Controllers;

public class UsersControllerAdditionalTests
{
    private static readonly Guid _tenantGuid = Guid.NewGuid();
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
    }

    [Fact]
    public async Task GetUserById_WhenUserNotInTenant_ReturnsNotFound()
    {
        Guid userId = Guid.NewGuid();
        UserDto user = new(userId, "a@test.com", "Alice", "Smith", true, []);
        _userManagement.GetUserByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(user);
        // Return organizations that do NOT include our tenant
        _organizationService.GetUserOrganizationsAsync(userId, Arg.Any<CancellationToken>())
            .Returns(new List<OrganizationDto> { new(Guid.NewGuid(), "Other Org", null, 1) });

        ActionResult<UserDto> result = await _controller.GetUserById(userId, CancellationToken.None);

        result.Result.Should().BeOfType<NotFoundResult>();
    }

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
    public async Task GetUsers_WithMaxZero_StillReturnsPage1()
    {
        _userQueryService.SearchUsersAsync(_tenantGuid, null, 0, 0, Arg.Any<CancellationToken>())
            .Returns(new UserSearchPageResult([], 0, 0, 0));

        ActionResult<Wallow.Shared.Kernel.Pagination.PagedResult<UserDto>> result =
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

        ActionResult<Wallow.Shared.Kernel.Pagination.PagedResult<UserDto>> result =
            await _controller.GetUsers("Jones", 0, 10, CancellationToken.None);

        OkObjectResult ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        Wallow.Shared.Kernel.Pagination.PagedResult<UserDto> page =
            ok.Value.Should().BeOfType<Wallow.Shared.Kernel.Pagination.PagedResult<UserDto>>().Subject;
        page.Items.Should().HaveCount(1);
        page.Items[0].LastName.Should().Be("Jones");
    }
}
