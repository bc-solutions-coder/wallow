using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Wallow.Identity.Api.Controllers;
using Wallow.Identity.Application.DTOs;
using Wallow.Identity.Application.Interfaces;

namespace Wallow.Identity.Tests.Api.Controllers;

/// <summary>
/// The caller's own memberships, answered from the token's subject and nothing else. Reading it
/// off the ambient tenant instead would answer a different question — one the caller could
/// already answer, since their app is bound to that organization.
/// </summary>
public class MeControllerTests
{
    private readonly IOrganizationService _orgService = Substitute.For<IOrganizationService>();
    private readonly MeController _controller;
    private readonly Guid _userId = Guid.NewGuid();

    public MeControllerTests()
    {
        _controller = new MeController(_orgService);

        ClaimsPrincipal user = new(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, _userId.ToString())], "TestAuth"));
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user }
        };
    }

    [Fact]
    public async Task GetOrganizations_AsksForTheSignedInUsersOwnMemberships()
    {
        List<MyOrganizationDto> organizations =
        [
            new MyOrganizationDto(Guid.NewGuid(), "Acme", "acme", true),
            new MyOrganizationDto(Guid.NewGuid(), "Globex", "globex", false)
        ];
        _orgService.GetMyOrganizationsAsync(_userId, Arg.Any<CancellationToken>())
            .Returns(organizations);

        ActionResult<IReadOnlyList<MyOrganizationDto>> result =
            await _controller.GetOrganizations(CancellationToken.None);

        OkObjectResult ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeEquivalentTo(organizations);
    }

    [Fact]
    public async Task GetOrganizations_BelongingToNothing_IsAnEmptyListRatherThanARefusal()
    {
        _orgService.GetMyOrganizationsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns([]);

        ActionResult<IReadOnlyList<MyOrganizationDto>> result =
            await _controller.GetOrganizations(CancellationToken.None);

        OkObjectResult ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeAssignableTo<IReadOnlyList<MyOrganizationDto>>().Which.Should().BeEmpty();
    }
}
