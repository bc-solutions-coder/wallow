using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Wallow.Identity.Api.Contracts.Requests;
using Wallow.Identity.Api.Controllers;
using Wallow.Identity.Application.DTOs;
using Wallow.Identity.Application.Interfaces;
using Wallow.Shared.Contracts.Identity;
using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.MultiTenancy;

namespace Wallow.Identity.Tests.Api.Controllers;

/// <summary>
/// Global admin is provisioned only from seeded configuration (seed.json) and OpenIddict
/// application properties. UsersController.AssignRole is the tenant-facing role surface and
/// takes an unrestricted free string, so it must reject every spelling of the reserved
/// global-admin name outright rather than forward it to the role store (finding F5).
/// </summary>
public sealed class UsersControllerGlobalAdminTests
{
    private static readonly Guid _tenantId = Guid.NewGuid();

    private readonly IUserManagementService _userManagement = Substitute.For<IUserManagementService>();
    private readonly IOrganizationService _organizationService = Substitute.For<IOrganizationService>();
    private readonly IUserQueryService _userQueryService = Substitute.For<IUserQueryService>();
    private readonly UsersController _controller;
    private readonly Guid _targetUserId = Guid.NewGuid();

    public UsersControllerGlobalAdminTests()
    {
        ITenantContext tenantContext = Substitute.For<ITenantContext>();
        tenantContext.TenantId.Returns(new TenantId(_tenantId));

        _controller = new UsersController(_userManagement, _organizationService, _userQueryService, tenantContext)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [
                            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
                            new Claim("org_id", _tenantId.ToString()),
                            new Claim(ClaimTypes.Role, "admin"),
                            new Claim("permission", "RolesUpdate"),
                        ],
                        "test")),
                },
            },
        };

        _organizationService.GetUserOrganizationsAsync(_targetUserId, Arg.Any<CancellationToken>())
            .Returns(new List<OrganizationDto> { new(_tenantId, "Test Org", null, 1) });
    }

    [Theory]
    [InlineData("global-admin")]
    [InlineData("global_admin")]
    [InlineData("globaladmin")]
    [InlineData("GlobalAdmin")]
    [InlineData("GLOBAL_ADMIN")]
    [InlineData("Global-Admin")]
    [InlineData("is_global_admin")]
    [InlineData("  global-admin  ")]
    public async Task AssignRole_ReservedGlobalAdminName_IsRejected(string roleName)
    {
        ActionResult result = await _controller.AssignRole(
            _targetUserId,
            new AssignRoleRequest(roleName),
            CancellationToken.None);

        result.Should().BeAssignableTo<IStatusCodeActionResult>();
        ((IStatusCodeActionResult)result).StatusCode.Should().Be(
            StatusCodes.Status400BadRequest,
            "the global-admin name is reserved and cannot be granted through a tenant-facing endpoint");
    }

    [Theory]
    [InlineData("global-admin")]
    [InlineData("global_admin")]
    [InlineData("globaladmin")]
    [InlineData("GlobalAdmin")]
    [InlineData("GLOBAL_ADMIN")]
    [InlineData("Global-Admin")]
    [InlineData("is_global_admin")]
    [InlineData("  global-admin  ")]
    public async Task AssignRole_ReservedGlobalAdminName_NeverReachesTheRoleStore(string roleName)
    {
        await _controller.AssignRole(
            _targetUserId,
            new AssignRoleRequest(roleName),
            CancellationToken.None);

        await _userManagement.DidNotReceive().AssignRoleAsync(
            Arg.Any<Guid>(),
            Arg.Any<Guid>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("admin")]
    [InlineData("manager")]
    [InlineData("user")]
    public async Task AssignRole_OrdinaryTenantRole_StillAssigns(string roleName)
    {
        ActionResult result = await _controller.AssignRole(
            _targetUserId,
            new AssignRoleRequest(roleName),
            CancellationToken.None);

        result.Should().BeOfType<NoContentResult>(
            "tenant-scoped roles stay assignable; only the reserved global-admin name is blocked");
        await _userManagement.Received(1).AssignRoleAsync(
            _targetUserId,
            Arg.Any<Guid>(),
            roleName,
            Arg.Any<CancellationToken>());
    }
}
