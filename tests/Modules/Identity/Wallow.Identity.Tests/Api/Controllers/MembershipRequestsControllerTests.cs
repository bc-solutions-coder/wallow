using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Wallow.Identity.Api.Controllers;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Domain.Identity;
using Wallow.Shared.Kernel.Identity;

namespace Wallow.Identity.Tests.Api.Controllers;

public class MembershipRequestsControllerTests
{
    private readonly IDomainAssignmentService _domainAssignmentService;
    private readonly IMembershipRequestRepository _membershipRequestRepository;
    private readonly MembershipRequestsController _controller;
    private readonly Guid _userId = Guid.NewGuid();

    public MembershipRequestsControllerTests()
    {
        _domainAssignmentService = Substitute.For<IDomainAssignmentService>();
        _membershipRequestRepository = Substitute.For<IMembershipRequestRepository>();
        _controller = new MembershipRequestsController(_domainAssignmentService, _membershipRequestRepository);

        ClaimsPrincipal user = new(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, _userId.ToString())
        }, "test"));
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user }
        };
    }

    #region RequestMembership

    [Fact]
    public async Task RequestMembership_WithValidDomain_ReturnsCreatedAtAction()
    {
        Guid requestId = Guid.NewGuid();
        _domainAssignmentService.RequestMembershipAsync(_userId, "example.com", Arg.Any<CancellationToken>())
            .Returns(requestId);

        CreateMembershipRequest request = new("example.com");

        ActionResult result = await _controller.RequestMembership(request, CancellationToken.None);

        CreatedAtActionResult created = result.Should().BeOfType<CreatedAtActionResult>().Subject;
        created.ActionName.Should().Be(nameof(MembershipRequestsController.GetById));
    }

    [Fact]
    public async Task RequestMembership_WithValidDomain_ReturnsRequestIdInRouteValues()
    {
        Guid requestId = Guid.NewGuid();
        _domainAssignmentService.RequestMembershipAsync(_userId, "example.com", Arg.Any<CancellationToken>())
            .Returns(requestId);

        CreateMembershipRequest request = new("example.com");

        ActionResult result = await _controller.RequestMembership(request, CancellationToken.None);

        CreatedAtActionResult created = result.Should().BeOfType<CreatedAtActionResult>().Subject;
        created.RouteValues.Should().ContainKey("id").WhoseValue.Should().Be(requestId);
    }

    [Fact]
    public async Task RequestMembership_CallsServiceWithUserIdFromClaims()
    {
        _domainAssignmentService.RequestMembershipAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Guid.NewGuid());

        CreateMembershipRequest request = new("test.org");

        await _controller.RequestMembership(request, CancellationToken.None);

        await _domainAssignmentService.Received(1)
            .RequestMembershipAsync(_userId, "test.org", Arg.Any<CancellationToken>());
    }

    #endregion

    #region GetById

    [Fact]
    public async Task GetById_WhenExists_ReturnsOkWithMappedResponse()
    {
        Guid id = Guid.NewGuid();
        MembershipRequest entity = MembershipRequest.Create(
            TenantId.New(), _userId, "example.com", _userId, TimeProvider.System);
        _membershipRequestRepository.GetByIdAsync(Arg.Any<MembershipRequestId>(), Arg.Any<CancellationToken>())
            .Returns(entity);

        ActionResult result = await _controller.GetById(id, CancellationToken.None);

        OkObjectResult ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().NotBeNull();
    }

    [Fact]
    public async Task GetById_WhenExists_ResponseContainsExpectedFields()
    {
        Guid id = Guid.NewGuid();
        MembershipRequest entity = MembershipRequest.Create(
            TenantId.New(), _userId, "example.com", _userId, TimeProvider.System);
        _membershipRequestRepository.GetByIdAsync(Arg.Any<MembershipRequestId>(), Arg.Any<CancellationToken>())
            .Returns(entity);

        ActionResult result = await _controller.GetById(id, CancellationToken.None);

        OkObjectResult ok = result.Should().BeOfType<OkObjectResult>().Subject;
        object response = ok.Value!;
        System.Type responseType = response.GetType();
        responseType.GetProperty("id")!.GetValue(response).Should().Be(entity.Id.Value);
        responseType.GetProperty("userId")!.GetValue(response).Should().Be(_userId);
        responseType.GetProperty("emailDomain")!.GetValue(response).Should().Be("example.com");
        responseType.GetProperty("status")!.GetValue(response).Should().Be("Pending");
    }

    [Fact]
    public async Task GetById_WhenNotFound_ReturnsNotFound()
    {
        _membershipRequestRepository.GetByIdAsync(Arg.Any<MembershipRequestId>(), Arg.Any<CancellationToken>())
            .Returns((MembershipRequest?)null);

        ActionResult result = await _controller.GetById(Guid.NewGuid(), CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetById_CallsRepositoryWithCorrectId()
    {
        Guid id = Guid.NewGuid();
        _membershipRequestRepository.GetByIdAsync(Arg.Any<MembershipRequestId>(), Arg.Any<CancellationToken>())
            .Returns((MembershipRequest?)null);

        await _controller.GetById(id, CancellationToken.None);

        await _membershipRequestRepository.Received(1)
            .GetByIdAsync(MembershipRequestId.Create(id), Arg.Any<CancellationToken>());
    }

    #endregion

    #region GetPending

    [Fact]
    public async Task GetPending_ReturnsOkWithMappedResults()
    {
        List<MembershipRequest> requests =
        [
            MembershipRequest.Create(TenantId.New(), Guid.NewGuid(), "a.com", _userId, TimeProvider.System),
            MembershipRequest.Create(TenantId.New(), Guid.NewGuid(), "b.com", _userId, TimeProvider.System)
        ];
        _membershipRequestRepository.GetPendingAsync(0, 20, Arg.Any<CancellationToken>())
            .Returns(requests);

        ActionResult result = await _controller.GetPending(0, 20, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetPending_WithEmptyList_ReturnsOkWithEmptyCollection()
    {
        _membershipRequestRepository.GetPendingAsync(0, 20, Arg.Any<CancellationToken>())
            .Returns(new List<MembershipRequest>());

        ActionResult result = await _controller.GetPending(0, 20, CancellationToken.None);

        OkObjectResult ok = result.Should().BeOfType<OkObjectResult>().Subject;
        IEnumerable<object> items = ok.Value.Should().BeAssignableTo<IEnumerable<object>>().Subject;
        items.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPending_WithCustomSkipAndTake_PassesParametersToRepository()
    {
        _membershipRequestRepository.GetPendingAsync(5, 10, Arg.Any<CancellationToken>())
            .Returns(new List<MembershipRequest>());

        await _controller.GetPending(5, 10, CancellationToken.None);

        await _membershipRequestRepository.Received(1)
            .GetPendingAsync(5, 10, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetPending_UsesDefaultSkipAndTake()
    {
        _membershipRequestRepository.GetPendingAsync(0, 20, Arg.Any<CancellationToken>())
            .Returns(new List<MembershipRequest>());

        await _controller.GetPending(ct: CancellationToken.None);

        await _membershipRequestRepository.Received(1)
            .GetPendingAsync(0, 20, Arg.Any<CancellationToken>());
    }

    #endregion

    #region Approve

    [Fact]
    public async Task Approve_ReturnsNoContent()
    {
        Guid id = Guid.NewGuid();
        Guid orgId = Guid.NewGuid();
        ApproveMembershipRequest request = new(orgId);

        ActionResult result = await _controller.Approve(id, request, CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task Approve_CallsServiceWithCorrectParameters()
    {
        Guid id = Guid.NewGuid();
        Guid orgId = Guid.NewGuid();
        ApproveMembershipRequest request = new(orgId);

        await _controller.Approve(id, request, CancellationToken.None);

        await _domainAssignmentService.Received(1)
            .ApproveMembershipRequestAsync(id, orgId, Arg.Any<CancellationToken>());
    }

    #endregion

    #region Reject

    [Fact]
    public async Task Reject_ReturnsNoContent()
    {
        Guid id = Guid.NewGuid();

        ActionResult result = await _controller.Reject(id, CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task Reject_CallsServiceWithCorrectId()
    {
        Guid id = Guid.NewGuid();

        await _controller.Reject(id, CancellationToken.None);

        await _domainAssignmentService.Received(1)
            .RejectMembershipRequestAsync(id, Arg.Any<CancellationToken>());
    }

    #endregion
}
