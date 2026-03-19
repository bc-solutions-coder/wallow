using System.Security.Claims;
using Foundry.Identity.Api.Contracts.Requests;
using Foundry.Identity.Api.Contracts.Responses;
using Foundry.Identity.Api.Controllers;
using Foundry.Identity.Application.DTOs;
using Foundry.Identity.Application.Interfaces;
using Foundry.Shared.Kernel.Identity;
using Foundry.Shared.Kernel.MultiTenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Foundry.Identity.Tests.Api.Controllers;

public class OrganizationsControllerTests
{
    private static readonly string[] _userRole = ["user"];
    private readonly IOrganizationService _orgService;
    private readonly ITenantContext _tenantContext;
    private readonly OrganizationsController _controller;
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _tenantOrgId = Guid.NewGuid();

    public OrganizationsControllerTests()
    {
        _orgService = Substitute.For<IOrganizationService>();
        _tenantContext = Substitute.For<ITenantContext>();
        _tenantContext.TenantId.Returns(TenantId.Create(_tenantOrgId));
        _controller = new OrganizationsController(_orgService, _tenantContext);

        ClaimsPrincipal user = new(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, _userId.ToString()),
            new Claim(ClaimTypes.Email, "creator@test.com")
        }, "TestAuth"));
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user }
        };
    }

    #region Create

    [Fact]
    public async Task Create_WithValidRequest_ReturnsCreatedAtAction()
    {
        Guid orgId = Guid.NewGuid();
        CreateOrganizationRequest request = new("Acme Corp", "acme.com");
        _orgService.CreateOrganizationAsync("Acme Corp", "acme.com", "creator@test.com", Arg.Any<CancellationToken>())
            .Returns(orgId);

        ActionResult<CreateOrganizationResponse> result = await _controller.Create(request, CancellationToken.None);

        CreatedAtActionResult created = result.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
        created.ActionName.Should().Be(nameof(OrganizationsController.GetById));
        created.RouteValues!["id"].Should().Be(orgId);
        CreateOrganizationResponse response = created.Value.Should().BeOfType<CreateOrganizationResponse>().Subject;
        response.OrganizationId.Should().Be(orgId);
    }

    [Fact]
    public async Task Create_WithNullDomain_PassesNullToService()
    {
        Guid orgId = Guid.NewGuid();
        CreateOrganizationRequest request = new("No Domain Org", null);
        _orgService.CreateOrganizationAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(orgId);

        await _controller.Create(request, CancellationToken.None);

        await _orgService.Received(1).CreateOrganizationAsync("No Domain Org", null, "creator@test.com", Arg.Any<CancellationToken>());
    }

    #endregion

    #region GetAll

    [Fact]
    public async Task GetAll_ReturnsOkFilteredToCurrentTenant()
    {
        List<OrganizationDto> orgs =
        [
            new OrganizationDto(_tenantOrgId, "Tenant Org", "tenant.com", 5),
            new OrganizationDto(Guid.NewGuid(), "Other Org", "other.com", 3)
        ];
        _orgService.GetOrganizationsAsync(null, 0, 20, Arg.Any<CancellationToken>())
            .Returns(orgs);

        ActionResult<IReadOnlyList<OrganizationDto>> result = await _controller.GetAll(null, 0, 20, CancellationToken.None);

        OkObjectResult ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        IReadOnlyList<OrganizationDto> response = ok.Value.Should().BeAssignableTo<IReadOnlyList<OrganizationDto>>().Subject;
        response.Should().HaveCount(1);
        response[0].Id.Should().Be(_tenantOrgId);
    }

    [Fact]
    public async Task GetAll_WithSearchAndPagination_PassesParametersToService()
    {
        _orgService.GetOrganizationsAsync(Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([]);

        await _controller.GetAll("acme", 10, 50, CancellationToken.None);

        await _orgService.Received(1).GetOrganizationsAsync("acme", 10, 50, Arg.Any<CancellationToken>());
    }

    #endregion

    #region GetById

    [Fact]
    public async Task GetById_WhenFound_ReturnsOk()
    {
        OrganizationDto org = new(_tenantOrgId, "Acme Corp", "acme.com", 10);
        _orgService.GetOrganizationByIdAsync(_tenantOrgId, Arg.Any<CancellationToken>())
            .Returns(org);

        ActionResult<OrganizationDto> result = await _controller.GetById(_tenantOrgId, CancellationToken.None);

        OkObjectResult ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        OrganizationDto response = ok.Value.Should().BeOfType<OrganizationDto>().Subject;
        response.Id.Should().Be(_tenantOrgId);
        response.Name.Should().Be("Acme Corp");
    }

    [Fact]
    public async Task GetById_WhenNotFound_ReturnsNotFound()
    {
        _orgService.GetOrganizationByIdAsync(_tenantOrgId, Arg.Any<CancellationToken>())
            .Returns((OrganizationDto?)null);

        ActionResult<OrganizationDto> result = await _controller.GetById(_tenantOrgId, CancellationToken.None);

        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetById_WhenOrgIsNotCurrentTenant_ReturnsNotFound()
    {
        Guid otherOrgId = Guid.NewGuid();

        ActionResult<OrganizationDto> result = await _controller.GetById(otherOrgId, CancellationToken.None);

        result.Result.Should().BeOfType<NotFoundResult>();
        await _orgService.DidNotReceive().GetOrganizationByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    #endregion

    #region GetMembers

    [Fact]
    public async Task GetMembers_ReturnsOkWithMemberList()
    {
        List<UserDto> members =
        [
            new UserDto(Guid.NewGuid(), "a@test.com", "Alice", "Smith", true, _userRole)
        ];
        _orgService.GetMembersAsync(_tenantOrgId, Arg.Any<CancellationToken>())
            .Returns(members);

        ActionResult<IReadOnlyList<UserDto>> result = await _controller.GetMembers(_tenantOrgId, CancellationToken.None);

        OkObjectResult ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        IReadOnlyList<UserDto> response = ok.Value.Should().BeAssignableTo<IReadOnlyList<UserDto>>().Subject;
        response.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetMembers_WhenOrgIsNotCurrentTenant_ReturnsNotFound()
    {
        Guid otherOrgId = Guid.NewGuid();

        ActionResult<IReadOnlyList<UserDto>> result = await _controller.GetMembers(otherOrgId, CancellationToken.None);

        result.Result.Should().BeOfType<NotFoundResult>();
        await _orgService.DidNotReceive().GetMembersAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    #endregion

    #region AddMember

    [Fact]
    public async Task AddMember_ReturnsNoContent()
    {
        Guid memberId = Guid.NewGuid();
        AddMemberRequest request = new(memberId);

        ActionResult result = await _controller.AddMember(_tenantOrgId, request, CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
        await _orgService.Received(1).AddMemberAsync(_tenantOrgId, memberId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AddMember_WhenOrgIsNotCurrentTenant_ReturnsNotFound()
    {
        Guid otherOrgId = Guid.NewGuid();
        AddMemberRequest request = new(Guid.NewGuid());

        ActionResult result = await _controller.AddMember(otherOrgId, request, CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
        await _orgService.DidNotReceive().AddMemberAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    #endregion

    #region RemoveMember

    [Fact]
    public async Task RemoveMember_ReturnsNoContent()
    {
        Guid memberId = Guid.NewGuid();

        ActionResult result = await _controller.RemoveMember(_tenantOrgId, memberId, CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
        await _orgService.Received(1).RemoveMemberAsync(_tenantOrgId, memberId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RemoveMember_WhenOrgIsNotCurrentTenant_ReturnsNotFound()
    {
        Guid otherOrgId = Guid.NewGuid();
        Guid memberId = Guid.NewGuid();

        ActionResult result = await _controller.RemoveMember(otherOrgId, memberId, CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
        await _orgService.DidNotReceive().RemoveMemberAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    #endregion

    #region GetMyOrganizations

    [Fact]
    public async Task GetMyOrganizations_ReturnsOkWithCurrentUserOrgs()
    {
        List<OrganizationDto> orgs =
        [
            new OrganizationDto(Guid.NewGuid(), "My Org", null, 5)
        ];
        _orgService.GetUserOrganizationsAsync(_userId, Arg.Any<CancellationToken>())
            .Returns(orgs);

        ActionResult<IReadOnlyList<OrganizationDto>> result = await _controller.GetMyOrganizations(CancellationToken.None);

        OkObjectResult ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        IReadOnlyList<OrganizationDto> response = ok.Value.Should().BeAssignableTo<IReadOnlyList<OrganizationDto>>().Subject;
        response.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetMyOrganizations_UsesCurrentUserIdFromClaims()
    {
        _orgService.GetUserOrganizationsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns([]);

        await _controller.GetMyOrganizations(CancellationToken.None);

        await _orgService.Received(1).GetUserOrganizationsAsync(_userId, Arg.Any<CancellationToken>());
    }

    #endregion
}
