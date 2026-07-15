using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Wallow.Identity.Api.Contracts.Requests;
using Wallow.Identity.Api.Contracts.Responses;
using Wallow.Identity.Api.Controllers;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Entities;
using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.MultiTenancy;

namespace Wallow.Identity.Tests.Api.Controllers;

public class InvitationsControllerTests
{
    private readonly IInvitationService _invitationService;
    private readonly IInvitationRepository _invitationRepository;
    private readonly ITenantContext _tenantContext;
    private readonly InvitationsController _controller;
    private readonly Guid _userId = Guid.NewGuid();
    private readonly TenantId _tenantId = TenantId.New();

    public InvitationsControllerTests()
    {
        _invitationService = Substitute.For<IInvitationService>();
        _invitationRepository = Substitute.For<IInvitationRepository>();
        _tenantContext = Substitute.For<ITenantContext>();
        _tenantContext.TenantId.Returns(_tenantId);

        _controller = new InvitationsController(_invitationService, _invitationRepository, _tenantContext);

        ClaimsPrincipal user = new(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, _userId.ToString())
        }, "test"));
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user }
        };
    }

    private Invitation CreateTestInvitation(string email = "test@example.com")
    {
        return Invitation.Create(
            _tenantId, email, DateTimeOffset.UtcNow.AddDays(7), _userId, TimeProvider.System);
    }

    #region Create

    [Fact]
    public async Task Create_ReturnsCreatedAtAction_WithInvitationResponse()
    {
        Invitation invitation = CreateTestInvitation();
        _invitationService.CreateInvitationAsync(_tenantId.Value, "test@example.com", _userId, Arg.Any<CancellationToken>())
            .Returns(invitation);

        CreateInvitationRequest request = new("test@example.com");

        ActionResult<InvitationResponse> result = await _controller.Create(request, CancellationToken.None);

        CreatedAtActionResult created = result.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
        created.ActionName.Should().Be(nameof(InvitationsController.Verify));
        InvitationResponse response = created.Value.Should().BeOfType<InvitationResponse>().Subject;
        response.Email.Should().Be("test@example.com");
    }

    [Fact]
    public async Task Create_MapsAllResponseFields_Correctly()
    {
        Invitation invitation = CreateTestInvitation("mapped@example.com");
        _invitationService.CreateInvitationAsync(_tenantId.Value, "mapped@example.com", _userId, Arg.Any<CancellationToken>())
            .Returns(invitation);

        CreateInvitationRequest request = new("mapped@example.com");

        ActionResult<InvitationResponse> result = await _controller.Create(request, CancellationToken.None);

        CreatedAtActionResult created = result.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
        InvitationResponse response = created.Value.Should().BeOfType<InvitationResponse>().Subject;
        response.Id.Should().Be(invitation.Id.Value);
        response.Email.Should().Be("mapped@example.com");
        response.Status.Should().Be("Pending");
        response.ExpiresAt.Should().Be(invitation.ExpiresAt);
        response.CreatedAt.Should().Be(invitation.CreatedAt);
        response.AcceptedByUserId.Should().BeNull();
    }

    [Fact]
    public async Task Create_RouteValues_ContainToken()
    {
        Invitation invitation = CreateTestInvitation();
        _invitationService.CreateInvitationAsync(_tenantId.Value, "test@example.com", _userId, Arg.Any<CancellationToken>())
            .Returns(invitation);

        CreateInvitationRequest request = new("test@example.com");

        ActionResult<InvitationResponse> result = await _controller.Create(request, CancellationToken.None);

        CreatedAtActionResult created = result.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
        created.RouteValues.Should().ContainKey("token");
        created.RouteValues!["token"].Should().Be(invitation.Token);
    }

    [Fact]
    public async Task Create_PassesCorrectArguments_ToService()
    {
        Invitation invitation = CreateTestInvitation("verify@example.com");
        _invitationService.CreateInvitationAsync(
                Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(invitation);

        CreateInvitationRequest request = new("verify@example.com");

        await _controller.Create(request, CancellationToken.None);

        await _invitationService.Received(1).CreateInvitationAsync(
            _tenantId.Value, "verify@example.com", _userId, Arg.Any<CancellationToken>());
    }

    #endregion

    #region GetByTenant

    [Fact]
    public async Task GetByTenant_ReturnsOk_WithInvitationList()
    {
        List<Invitation> invitations =
        [
            CreateTestInvitation("a@test.com"),
            CreateTestInvitation("b@test.com")
        ];
        _invitationRepository.GetPagedByTenantAsync(_tenantId.Value, 0, 20, Arg.Any<CancellationToken>())
            .Returns(invitations);

        ActionResult<List<InvitationResponse>> result = await _controller.GetByTenant(0, 20, CancellationToken.None);

        OkObjectResult ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        List<InvitationResponse> responses = ok.Value.Should().BeOfType<List<InvitationResponse>>().Subject;
        responses.Count.Should().Be(2);
    }

    [Fact]
    public async Task GetByTenant_WhenEmpty_ReturnsOk_WithEmptyList()
    {
        _invitationRepository.GetPagedByTenantAsync(_tenantId.Value, 0, 20, Arg.Any<CancellationToken>())
            .Returns(new List<Invitation>());

        ActionResult<List<InvitationResponse>> result = await _controller.GetByTenant(0, 20, CancellationToken.None);

        OkObjectResult ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        List<InvitationResponse> responses = ok.Value.Should().BeOfType<List<InvitationResponse>>().Subject;
        responses.Should().BeEmpty();
    }

    [Fact]
    public async Task GetByTenant_PassesSkipAndTake_ToRepository()
    {
        _invitationRepository.GetPagedByTenantAsync(_tenantId.Value, 10, 5, Arg.Any<CancellationToken>())
            .Returns(new List<Invitation>());

        await _controller.GetByTenant(10, 5, CancellationToken.None);

        await _invitationRepository.Received(1).GetPagedByTenantAsync(
            _tenantId.Value, 10, 5, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetByTenant_MapsResponseFields_ForEachInvitation()
    {
        Invitation invitation = CreateTestInvitation("fields@test.com");
        _invitationRepository.GetPagedByTenantAsync(_tenantId.Value, 0, 20, Arg.Any<CancellationToken>())
            .Returns(new List<Invitation> { invitation });

        ActionResult<List<InvitationResponse>> result = await _controller.GetByTenant(0, 20, CancellationToken.None);

        OkObjectResult ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        List<InvitationResponse> responses = ok.Value.Should().BeOfType<List<InvitationResponse>>().Subject;
        InvitationResponse response = responses.Single();
        response.Id.Should().Be(invitation.Id.Value);
        response.Email.Should().Be("fields@test.com");
        response.Status.Should().Be("Pending");
        response.ExpiresAt.Should().Be(invitation.ExpiresAt);
        response.CreatedAt.Should().Be(invitation.CreatedAt);
        response.AcceptedByUserId.Should().BeNull();
    }

    #endregion

    #region Revoke

    [Fact]
    public async Task Revoke_ReturnsNoContent()
    {
        Guid id = Guid.NewGuid();

        ActionResult result = await _controller.Revoke(id, CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
        await _invitationService.Received(1).RevokeInvitationAsync(id, _userId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Revoke_PassesCorrectUserId_ToService()
    {
        Guid invitationId = Guid.NewGuid();

        await _controller.Revoke(invitationId, CancellationToken.None);

        await _invitationService.Received(1).RevokeInvitationAsync(
            invitationId, _userId, Arg.Any<CancellationToken>());
    }

    #endregion

    #region Verify

    [Fact]
    public async Task Verify_WhenExists_ReturnsOk()
    {
        Invitation invitation = CreateTestInvitation();
        _invitationService.GetInvitationByTokenAsync("valid-token", Arg.Any<CancellationToken>())
            .Returns(invitation);

        ActionResult<InvitationResponse> result = await _controller.Verify("valid-token", CancellationToken.None);

        OkObjectResult ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        InvitationResponse response = ok.Value.Should().BeOfType<InvitationResponse>().Subject;
        response.Email.Should().Be("test@example.com");
    }

    [Fact]
    public async Task Verify_WhenExists_MapsAllFields()
    {
        Invitation invitation = CreateTestInvitation("verify-fields@example.com");
        _invitationService.GetInvitationByTokenAsync("token-123", Arg.Any<CancellationToken>())
            .Returns(invitation);

        ActionResult<InvitationResponse> result = await _controller.Verify("token-123", CancellationToken.None);

        OkObjectResult ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        InvitationResponse response = ok.Value.Should().BeOfType<InvitationResponse>().Subject;
        response.Id.Should().Be(invitation.Id.Value);
        response.Email.Should().Be("verify-fields@example.com");
        response.Status.Should().Be("Pending");
        response.ExpiresAt.Should().Be(invitation.ExpiresAt);
        response.CreatedAt.Should().Be(invitation.CreatedAt);
    }

    [Fact]
    public async Task Verify_WhenNotFound_ReturnsNotFound()
    {
        _invitationService.GetInvitationByTokenAsync("invalid-token", Arg.Any<CancellationToken>())
            .Returns((Invitation?)null);

        ActionResult<InvitationResponse> result = await _controller.Verify("invalid-token", CancellationToken.None);

        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task Verify_PassesToken_ToService()
    {
        _invitationService.GetInvitationByTokenAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((Invitation?)null);

        await _controller.Verify("specific-token", CancellationToken.None);

        await _invitationService.Received(1).GetInvitationByTokenAsync(
            "specific-token", Arg.Any<CancellationToken>());
    }

    #endregion

    #region Accept

    [Fact]
    public async Task Accept_ReturnsNoContent()
    {
        ActionResult result = await _controller.Accept("some-token", CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
        await _invitationService.Received(1).AcceptInvitationAsync("some-token", _userId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Accept_PassesCorrectTokenAndUserId_ToService()
    {
        await _controller.Accept("accept-token-xyz", CancellationToken.None);

        await _invitationService.Received(1).AcceptInvitationAsync(
            "accept-token-xyz", _userId, Arg.Any<CancellationToken>());
    }

    #endregion
}
