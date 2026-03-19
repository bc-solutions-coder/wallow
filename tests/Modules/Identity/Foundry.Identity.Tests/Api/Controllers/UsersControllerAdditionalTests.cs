using Foundry.Identity.Api.Contracts.Requests;
using Foundry.Identity.Api.Controllers;
using Foundry.Identity.Application.DTOs;
using Foundry.Identity.Application.Interfaces;
using Foundry.Shared.Kernel.Identity;
using Foundry.Shared.Kernel.MultiTenancy;
using Microsoft.AspNetCore.Mvc;

namespace Foundry.Identity.Tests.Api.Controllers;

public class UsersControllerAdditionalTests
{
    private static readonly Guid _tenantGuid = Guid.NewGuid();
    private readonly IUserManagementService _keycloakAdmin;
    private readonly IOrganizationService _keycloakOrg;
    private readonly UsersController _controller;

    public UsersControllerAdditionalTests()
    {
        _keycloakAdmin = Substitute.For<IUserManagementService>();
        _keycloakOrg = Substitute.For<IOrganizationService>();
        ITenantContext tenantContext = Substitute.For<ITenantContext>();
        tenantContext.TenantId.Returns(new TenantId(_tenantGuid));
        _controller = new UsersController(_keycloakAdmin, _keycloakOrg, tenantContext);
    }

    [Fact]
    public async Task GetUserById_WhenUserNotInTenant_ReturnsNotFound()
    {
        Guid userId = Guid.NewGuid();
        UserDto user = new(userId, "a@test.com", "Alice", "Smith", true, []);
        _keycloakAdmin.GetUserByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(user);
        // Return organizations that do NOT include our tenant
        _keycloakOrg.GetUserOrganizationsAsync(userId, Arg.Any<CancellationToken>())
            .Returns(new List<OrganizationDto> { new(Guid.NewGuid(), "Other Org", null, 1) });

        ActionResult<UserDto> result = await _controller.GetUserById(userId, CancellationToken.None);

        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task DeactivateUser_WhenUserNotInTenant_ReturnsNotFound()
    {
        Guid userId = Guid.NewGuid();
        _keycloakOrg.GetUserOrganizationsAsync(userId, Arg.Any<CancellationToken>())
            .Returns(new List<OrganizationDto> { new(Guid.NewGuid(), "Other Org", null, 1) });

        ActionResult result = await _controller.DeactivateUser(userId, CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
        await _keycloakAdmin.DidNotReceive().DeactivateUserAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ActivateUser_WhenUserNotInTenant_ReturnsNotFound()
    {
        Guid userId = Guid.NewGuid();
        _keycloakOrg.GetUserOrganizationsAsync(userId, Arg.Any<CancellationToken>())
            .Returns(new List<OrganizationDto>());

        ActionResult result = await _controller.ActivateUser(userId, CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
        await _keycloakAdmin.DidNotReceive().ActivateUserAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AssignRole_WhenUserNotInTenant_ReturnsNotFound()
    {
        Guid userId = Guid.NewGuid();
        AssignRoleRequest request = new("admin");
        _keycloakOrg.GetUserOrganizationsAsync(userId, Arg.Any<CancellationToken>())
            .Returns(new List<OrganizationDto>());

        ActionResult result = await _controller.AssignRole(userId, request, CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
        await _keycloakAdmin.DidNotReceive().AssignRoleAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RemoveRole_WhenUserNotInTenant_ReturnsNotFound()
    {
        Guid userId = Guid.NewGuid();
        _keycloakOrg.GetUserOrganizationsAsync(userId, Arg.Any<CancellationToken>())
            .Returns(new List<OrganizationDto>());

        ActionResult result = await _controller.RemoveRole(userId, "admin", CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
        await _keycloakAdmin.DidNotReceive().RemoveRoleAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetUsers_WithMaxZero_StillReturnsPage1()
    {
        _keycloakOrg.GetMembersAsync(_tenantGuid, Arg.Any<CancellationToken>())
            .Returns(new List<UserDto>());

        ActionResult<Foundry.Shared.Kernel.Pagination.PagedResult<UserDto>> result =
            await _controller.GetUsers(null, 0, 0, CancellationToken.None);

        result.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetUsers_SearchByLastName_FiltersResults()
    {
        List<UserDto> users =
        [
            new(Guid.NewGuid(), "alice@test.com", "Alice", "Smith", true, []),
            new(Guid.NewGuid(), "bob@test.com", "Bob", "Jones", true, [])
        ];
        _keycloakOrg.GetMembersAsync(_tenantGuid, Arg.Any<CancellationToken>())
            .Returns(users);

        ActionResult<Foundry.Shared.Kernel.Pagination.PagedResult<UserDto>> result =
            await _controller.GetUsers("Jones", 0, 10, CancellationToken.None);

        OkObjectResult ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        Foundry.Shared.Kernel.Pagination.PagedResult<UserDto> page =
            ok.Value.Should().BeOfType<Foundry.Shared.Kernel.Pagination.PagedResult<UserDto>>().Subject;
        page.Items.Should().HaveCount(1);
        page.Items[0].LastName.Should().Be("Jones");
    }
}
