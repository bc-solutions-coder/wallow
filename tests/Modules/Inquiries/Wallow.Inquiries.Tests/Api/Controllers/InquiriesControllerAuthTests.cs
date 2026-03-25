using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Wallow.Inquiries.Api.Contracts;
using Wallow.Inquiries.Api.Controllers;
using Wallow.Inquiries.Application.DTOs;
using Wallow.Inquiries.Application.Queries.GetInquiryById;
using Wallow.Inquiries.Application.Queries.GetInquiryComments;
using Wallow.Inquiries.Application.Queries.GetSubmittedInquiries;
using Wallow.Shared.Kernel.Identity.Authorization;
using Wallow.Shared.Kernel.MultiTenancy;
using Wallow.Shared.Kernel.Results;
using Wolverine;

namespace Wallow.Inquiries.Tests.Api.Controllers;

public class InquiriesControllerAuthTests
{
    private readonly IMessageBus _bus;
    private readonly InquiriesController _controller;

    public InquiriesControllerAuthTests()
    {
        _bus = Substitute.For<IMessageBus>();
        ITenantContext tenantContext = Substitute.For<ITenantContext>();
        _controller = new InquiriesController(_bus, tenantContext);
    }

    private void SetUser(string? sub = null, string? azp = null, params string[] permissions)
    {
        List<Claim> claims = [];
        if (sub is not null)
        {
            claims.Add(new Claim("sub", sub));
        }
        if (azp is not null)
        {
            claims.Add(new Claim("azp", azp));
        }
        foreach (string permission in permissions)
        {
            claims.Add(new Claim("permission", permission));
        }

        ClaimsIdentity identity = new(claims, "TestAuth");
        ClaimsPrincipal principal = new(identity);

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };
    }

    private static InquiryDto CreateDto(Guid? id = null, string? submitterId = null) =>
        new(
            id ?? Guid.NewGuid(),
            "John Doe",
            "john@example.com",
            "555-0100",
            "Acme Corp",
            submitterId,
            "Web App",
            "$10k",
            "3 months",
            "We need help.",
            "New",
            "1.2.3.4",
            DateTimeOffset.UtcNow);

    #region GetById Auth

    [Fact]
    public async Task GetById_AdminWithInquiriesRead_ReturnsOk()
    {
        Guid id = Guid.NewGuid();
        InquiryDto dto = CreateDto(id, submitterId: "other-user");
        SetUser(sub: "admin-user", permissions: PermissionType.InquiriesRead);

        _bus.InvokeAsync<Result<InquiryDto>>(Arg.Any<GetInquiryByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(dto));

        IActionResult result = await _controller.GetById(id, CancellationToken.None);

        OkObjectResult ok = result.Should().BeOfType<OkObjectResult>().Subject;
        InquiryResponse response = ok.Value.Should().BeOfType<InquiryResponse>().Subject;
        response.Id.Should().Be(id);
    }

    [Fact]
    public async Task GetById_OwnerMatchingSubmitterId_ReturnsOk()
    {
        Guid id = Guid.NewGuid();
        string submitterId = "owner-user";
        InquiryDto dto = CreateDto(id, submitterId: submitterId);
        SetUser(sub: submitterId);

        _bus.InvokeAsync<Result<InquiryDto>>(Arg.Any<GetInquiryByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(dto));

        IActionResult result = await _controller.GetById(id, CancellationToken.None);

        OkObjectResult ok = result.Should().BeOfType<OkObjectResult>().Subject;
        InquiryResponse response = ok.Value.Should().BeOfType<InquiryResponse>().Subject;
        response.Id.Should().Be(id);
    }

    [Fact]
    public async Task GetById_NonOwnerWithoutInquiriesRead_Returns404()
    {
        Guid id = Guid.NewGuid();
        InquiryDto dto = CreateDto(id, submitterId: "the-owner");
        SetUser(sub: "different-user");

        _bus.InvokeAsync<Result<InquiryDto>>(Arg.Any<GetInquiryByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(dto));

        IActionResult result = await _controller.GetById(id, CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetById_NoSubClaim_WithoutInquiriesRead_Returns404()
    {
        Guid id = Guid.NewGuid();
        InquiryDto dto = CreateDto(id, submitterId: "some-user");
        SetUser();

        _bus.InvokeAsync<Result<InquiryDto>>(Arg.Any<GetInquiryByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(dto));

        IActionResult result = await _controller.GetById(id, CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetById_ServiceAccountAzp_WithoutInquiriesRead_Returns404()
    {
        Guid id = Guid.NewGuid();
        InquiryDto dto = CreateDto(id, submitterId: "some-user");
        SetUser(sub: "sa-service", azp: "sa-service");

        _bus.InvokeAsync<Result<InquiryDto>>(Arg.Any<GetInquiryByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(dto));

        IActionResult result = await _controller.GetById(id, CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
    }

    #endregion

    #region GetSubmitted

    [Fact]
    public async Task GetSubmitted_ReturnsOnlyCallersInquiries()
    {
        string submitterId = "caller-user";
        List<InquiryDto> dtos = [CreateDto(submitterId: submitterId)];
        SetUser(sub: submitterId);

        _bus.InvokeAsync<Result<IReadOnlyList<InquiryDto>>>(
                Arg.Any<GetSubmittedInquiriesQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyList<InquiryDto>>(dtos));

        IActionResult result = await _controller.GetSubmitted(CancellationToken.None);

        OkObjectResult ok = result.Should().BeOfType<OkObjectResult>().Subject;
        IReadOnlyList<InquiryResponse> responses = ok.Value.Should().BeAssignableTo<IReadOnlyList<InquiryResponse>>().Subject;
        responses.Should().HaveCount(1);

        await _bus.Received(1).InvokeAsync<Result<IReadOnlyList<InquiryDto>>>(
            Arg.Is<GetSubmittedInquiriesQuery>(q => q.SubmitterId == submitterId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetSubmitted_WithNoSubClaim_ReturnsEmpty()
    {
        SetUser();

        IActionResult result = await _controller.GetSubmitted(CancellationToken.None);

        OkObjectResult ok = result.Should().BeOfType<OkObjectResult>().Subject;
        object[] responses = ok.Value.Should().BeAssignableTo<object[]>().Subject;
        responses.Should().BeEmpty();

        await _bus.DidNotReceive().InvokeAsync<Result<IReadOnlyList<InquiryDto>>>(
            Arg.Any<GetSubmittedInquiriesQuery>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetSubmitted_ServiceAccount_ReturnsEmpty()
    {
        SetUser(sub: "sa-client-id", azp: "sa-client-id");

        IActionResult result = await _controller.GetSubmitted(CancellationToken.None);

        OkObjectResult ok = result.Should().BeOfType<OkObjectResult>().Subject;
        object[] responses = ok.Value.Should().BeAssignableTo<object[]>().Subject;
        responses.Should().BeEmpty();

        await _bus.DidNotReceive().InvokeAsync<Result<IReadOnlyList<InquiryDto>>>(
            Arg.Any<GetSubmittedInquiriesQuery>(), Arg.Any<CancellationToken>());
    }

    #endregion

    #region GetComments Auth

    [Fact]
    public async Task GetComments_WithInquiriesRead_ReturnsOkWithInternalComments()
    {
        Guid inquiryId = Guid.NewGuid();
        SetUser(sub: "admin-user", permissions: PermissionType.InquiriesRead);

        _bus.InvokeAsync<IReadOnlyList<InquiryCommentDto>>(Arg.Any<GetInquiryCommentsQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<InquiryCommentDto>());

        IActionResult result = await _controller.GetComments(inquiryId, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();

        await _bus.Received(1).InvokeAsync<IReadOnlyList<InquiryCommentDto>>(
            Arg.Is<GetInquiryCommentsQuery>(q => q.IncludeInternal),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetComments_OwnerWithoutReadPermission_ReturnsOkWithoutInternalComments()
    {
        Guid inquiryId = Guid.NewGuid();
        string submitterId = "owner-user";
        SetUser(sub: submitterId);

        InquiryDto dto = CreateDto(inquiryId, submitterId: submitterId);
        _bus.InvokeAsync<Result<InquiryDto>>(Arg.Any<GetInquiryByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(dto));

        _bus.InvokeAsync<IReadOnlyList<InquiryCommentDto>>(Arg.Any<GetInquiryCommentsQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<InquiryCommentDto>());

        IActionResult result = await _controller.GetComments(inquiryId, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();

        await _bus.Received(1).InvokeAsync<IReadOnlyList<InquiryCommentDto>>(
            Arg.Is<GetInquiryCommentsQuery>(q => !q.IncludeInternal),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetComments_NonOwnerWithoutReadPermission_Returns404()
    {
        Guid inquiryId = Guid.NewGuid();
        SetUser(sub: "different-user");

        InquiryDto dto = CreateDto(inquiryId, submitterId: "the-owner");
        _bus.InvokeAsync<Result<InquiryDto>>(Arg.Any<GetInquiryByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(dto));

        IActionResult result = await _controller.GetComments(inquiryId, CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetComments_NoSubClaim_WithoutReadPermission_Returns404()
    {
        Guid inquiryId = Guid.NewGuid();
        SetUser();

        IActionResult result = await _controller.GetComments(inquiryId, CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetComments_ServiceAccount_WithoutReadPermission_Returns404()
    {
        Guid inquiryId = Guid.NewGuid();
        SetUser(sub: "sa-service", azp: "sa-service");

        IActionResult result = await _controller.GetComments(inquiryId, CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetComments_WhenInquiryNotFound_WithoutReadPermission_Returns404()
    {
        Guid inquiryId = Guid.NewGuid();
        SetUser(sub: "some-user");

        _bus.InvokeAsync<Result<InquiryDto>>(Arg.Any<GetInquiryByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<InquiryDto>(Error.NotFound("Inquiry", inquiryId)));

        IActionResult result = await _controller.GetComments(inquiryId, CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
    }

    #endregion
}
