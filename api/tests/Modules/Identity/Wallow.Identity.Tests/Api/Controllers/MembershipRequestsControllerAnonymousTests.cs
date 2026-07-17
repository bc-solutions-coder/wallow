using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Wallow.Identity.Api.Controllers;
using Wallow.Identity.Application.Interfaces;

namespace Wallow.Identity.Tests.Api.Controllers;

/// <summary>
/// Wallow-vec7.8: POST /v1/identity/membership-requests must stay authorized, and must fail
/// cleanly rather than throw when it is reached without a usable user id.
/// </summary>
public sealed class MembershipRequestsControllerAnonymousTests
{
    private readonly IDomainAssignmentService _domainAssignmentService;
    private readonly IMembershipRequestRepository _membershipRequestRepository;
    private readonly MembershipRequestsController _controller;

    public MembershipRequestsControllerAnonymousTests()
    {
        _domainAssignmentService = Substitute.For<IDomainAssignmentService>();
        _membershipRequestRepository = Substitute.For<IMembershipRequestRepository>();
        _controller = new MembershipRequestsController(_domainAssignmentService, _membershipRequestRepository);
    }

    /// <summary>Puts the given principal on the controller. An empty identity is anonymous.</summary>
    private void GivenPrincipal(ClaimsPrincipal principal)
    {
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };
    }

    /// <summary>
    /// The [Authorize] filter normally rejects this caller before the action runs, so the action
    /// body is a defence in depth: it must return 401, not throw a null/format exception.
    /// </summary>
    [Fact]
    public async Task RequestMembership_WithAnonymousPrincipal_ReturnsUnauthorized()
    {
        GivenPrincipal(new ClaimsPrincipal(new ClaimsIdentity()));

        ActionResult result = await _controller.RequestMembership(
            new CreateMembershipRequest("example.com"), CancellationToken.None);

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task RequestMembership_WithAnonymousPrincipal_EnqueuesNothing()
    {
        GivenPrincipal(new ClaimsPrincipal(new ClaimsIdentity()));

        ActionResult result = await _controller.RequestMembership(
            new CreateMembershipRequest("example.com"), CancellationToken.None);

        result.Should().BeOfType<UnauthorizedResult>();
        await _domainAssignmentService.DidNotReceive()
            .RequestMembershipAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RequestMembership_WithNonGuidUserIdClaim_ReturnsUnauthorized()
    {
        GivenPrincipal(new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.NameIdentifier, "not-a-guid") }, "test")));

        ActionResult result = await _controller.RequestMembership(
            new CreateMembershipRequest("example.com"), CancellationToken.None);

        result.Should().BeOfType<UnauthorizedResult>();
        await _domainAssignmentService.DidNotReceive()
            .RequestMembershipAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Locks the decision this bead made: the endpoint is NOT opened to anonymous callers, because
    /// an anonymous caller naming its own domain could enqueue requests against any organization.
    /// The registration flow goes through AccountController.Register instead.
    /// </summary>
    [Fact]
    public void RequestMembership_IsNotReachableAnonymously()
    {
        MethodInfo action = typeof(MembershipRequestsController)
            .GetMethod(nameof(MembershipRequestsController.RequestMembership))!;

        action.GetCustomAttribute<AllowAnonymousAttribute>().Should().BeNull();
        typeof(MembershipRequestsController).GetCustomAttribute<AuthorizeAttribute>().Should().NotBeNull();
    }
}
