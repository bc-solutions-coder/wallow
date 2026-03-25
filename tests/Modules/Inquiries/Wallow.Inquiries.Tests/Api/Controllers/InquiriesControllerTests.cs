using System.Security.Claims;
using Wallow.Inquiries.Api.Contracts;
using Wallow.Inquiries.Api.Controllers;
using Wallow.Inquiries.Application.Commands.AddInquiryComment;
using Wallow.Inquiries.Application.Commands.SubmitInquiry;
using Wallow.Inquiries.Application.Commands.UpdateInquiryStatus;
using Wallow.Inquiries.Application.DTOs;
using Wallow.Inquiries.Application.Queries.GetInquiries;
using Wallow.Inquiries.Application.Queries.GetInquiryById;
using Wallow.Inquiries.Application.Queries.GetInquiryComments;
using Wallow.Inquiries.Domain.Enums;
using Wallow.Inquiries.Domain.Identity;
using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Wallow.Shared.Kernel.MultiTenancy;
using Wolverine;

namespace Wallow.Inquiries.Tests.Api.Controllers;

public class InquiriesControllerTests
{
    private readonly IMessageBus _bus;
    private readonly ITenantContext _tenantContext;
    private readonly InquiriesController _controller;

    public InquiriesControllerTests()
    {
        _bus = Substitute.For<IMessageBus>();
        _tenantContext = Substitute.For<ITenantContext>();
        _tenantContext.TenantId.Returns(TenantId.Create(Guid.NewGuid()));
        _controller = new InquiriesController(_bus, _tenantContext);
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
    }

    private void SetUserWithClaims(string? sub = null, string? name = null, params string[] permissions)
    {
        List<Claim> claims = [];
        if (sub is not null)
        {
            claims.Add(new Claim("sub", sub));
        }

        if (name is not null)
        {
            claims.Add(new Claim("name", name));
        }

        foreach (string permission in permissions)
        {
            claims.Add(new Claim("permission", permission));
        }

        ClaimsIdentity identity = new(claims, "TestAuth");
        _controller.ControllerContext.HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(identity)
        };
    }

    private static InquiryDto CreateDto(Guid? id = null, string status = "New") =>
        new(
            id ?? Guid.NewGuid(),
            "John Doe",
            "john@example.com",
            "555-0100",
            "Acme Corp",
            null,
            "Web App",
            "$10k",
            "3 months",
            "We need help building our platform.",
            status,
            "1.2.3.4",
            DateTimeOffset.UtcNow);

    #region Submit

    [Fact]
    public async Task Submit_WithValidRequest_ReturnsOk()
    {
        InquiryDto dto = CreateDto();
        _bus.InvokeAsync<Result<InquiryDto>>(Arg.Any<SubmitInquiryCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(dto));

        SubmitInquiryRequest request = new("John Doe", "john@example.com", "555-0100", "Acme", "Website", "$10k", "3 months", "We need a website.");

        IActionResult result = await _controller.Submit(request, CancellationToken.None);

        OkObjectResult ok = result.Should().BeOfType<OkObjectResult>().Subject;
        InquiryResponse response = ok.Value.Should().BeOfType<InquiryResponse>().Subject;
        response.Name.Should().Be("John Doe");
        response.Email.Should().Be("john@example.com");
    }

    [Fact]
    public async Task Submit_WhenValidationFailure_Returns400()
    {
        _bus.InvokeAsync<Result<InquiryDto>>(Arg.Any<SubmitInquiryCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<InquiryDto>(Error.Validation("Name is required")));

        SubmitInquiryRequest request = new("", "john@example.com", "555-0100", null, "Website", "$10k", "3 months", "Message");

        IActionResult result = await _controller.Submit(request, CancellationToken.None);

        ObjectResult objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task Submit_MapsAllResponseFields()
    {
        Guid id = Guid.NewGuid();
        InquiryDto dto = CreateDto(id, "New");
        _bus.InvokeAsync<Result<InquiryDto>>(Arg.Any<SubmitInquiryCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(dto));

        SubmitInquiryRequest request = new("John Doe", "john@example.com", "555-0100", "Acme", "Website", "$10k", "3 months", "Message");

        IActionResult result = await _controller.Submit(request, CancellationToken.None);

        OkObjectResult ok = result.Should().BeOfType<OkObjectResult>().Subject;
        InquiryResponse response = ok.Value.Should().BeOfType<InquiryResponse>().Subject;
        response.Id.Should().Be(id);
        response.Status.Should().Be("New");
        response.Company.Should().Be("Acme Corp");
    }

    #endregion

    #region GetAll

    [Fact]
    public async Task GetAll_ReturnsOkWithAllInquiries()
    {
        List<InquiryDto> dtos = [CreateDto(), CreateDto()];
        _bus.InvokeAsync<Result<IReadOnlyList<InquiryDto>>>(Arg.Any<GetInquiriesQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyList<InquiryDto>>(dtos));

        IActionResult result = await _controller.GetAll(null, CancellationToken.None);

        OkObjectResult ok = result.Should().BeOfType<OkObjectResult>().Subject;
        IReadOnlyList<InquiryResponse> responses = ok.Value.Should().BeAssignableTo<IReadOnlyList<InquiryResponse>>().Subject;
        responses.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetAll_WithStatusFilter_PassesParsedStatusToQuery()
    {
        _bus.InvokeAsync<Result<IReadOnlyList<InquiryDto>>>(Arg.Any<GetInquiriesQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyList<InquiryDto>>([]));

        await _controller.GetAll("Reviewed", CancellationToken.None);

        await _bus.Received(1).InvokeAsync<Result<IReadOnlyList<InquiryDto>>>(
            Arg.Is<GetInquiriesQuery>(q => q.Status == InquiryStatus.Reviewed),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetAll_WithInvalidStatus_PassesNullStatusToQuery()
    {
        _bus.InvokeAsync<Result<IReadOnlyList<InquiryDto>>>(Arg.Any<GetInquiriesQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyList<InquiryDto>>([]));

        await _controller.GetAll("InvalidStatus", CancellationToken.None);

        await _bus.Received(1).InvokeAsync<Result<IReadOnlyList<InquiryDto>>>(
            Arg.Is<GetInquiriesQuery>(q => q.Status == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetAll_WithNoStatus_PassesNullStatusToQuery()
    {
        _bus.InvokeAsync<Result<IReadOnlyList<InquiryDto>>>(Arg.Any<GetInquiriesQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyList<InquiryDto>>([]));

        await _controller.GetAll(null, CancellationToken.None);

        await _bus.Received(1).InvokeAsync<Result<IReadOnlyList<InquiryDto>>>(
            Arg.Is<GetInquiriesQuery>(q => q.Status == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetAll_WithEmptyList_ReturnsOkWithEmptyList()
    {
        _bus.InvokeAsync<Result<IReadOnlyList<InquiryDto>>>(Arg.Any<GetInquiriesQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyList<InquiryDto>>([]));

        IActionResult result = await _controller.GetAll(null, CancellationToken.None);

        OkObjectResult ok = result.Should().BeOfType<OkObjectResult>().Subject;
        IReadOnlyList<InquiryResponse> responses = ok.Value.Should().BeAssignableTo<IReadOnlyList<InquiryResponse>>().Subject;
        responses.Should().BeEmpty();
    }

    #endregion

    #region GetById

    [Fact]
    public async Task GetById_WhenFound_ReturnsOkWithResponse()
    {
        Guid id = Guid.NewGuid();
        InquiryDto dto = CreateDto(id);
        _bus.InvokeAsync<Result<InquiryDto>>(Arg.Any<GetInquiryByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(dto));
        _controller.ControllerContext.HttpContext = new DefaultHttpContext
        {
            User = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(
            [
                new System.Security.Claims.Claim("permission", "InquiriesRead")
            ]))
        };

        IActionResult result = await _controller.GetById(id, CancellationToken.None);

        OkObjectResult ok = result.Should().BeOfType<OkObjectResult>().Subject;
        InquiryResponse response = ok.Value.Should().BeOfType<InquiryResponse>().Subject;
        response.Id.Should().Be(id);
    }

    [Fact]
    public async Task GetById_WhenNotFound_Returns404()
    {
        Guid id = Guid.NewGuid();
        _bus.InvokeAsync<Result<InquiryDto>>(Arg.Any<GetInquiryByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<InquiryDto>(Error.NotFound("Inquiry", id)));

        IActionResult result = await _controller.GetById(id, CancellationToken.None);

        ObjectResult objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task GetById_PassesCorrectIdToQuery()
    {
        Guid id = Guid.NewGuid();
        InquiryDto dto = CreateDto(id);
        _bus.InvokeAsync<Result<InquiryDto>>(Arg.Any<GetInquiryByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(dto));
        _controller.ControllerContext.HttpContext = new DefaultHttpContext
        {
            User = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(
            [
                new System.Security.Claims.Claim("permission", "InquiriesRead")
            ]))
        };

        await _controller.GetById(id, CancellationToken.None);

        await _bus.Received(1).InvokeAsync<Result<InquiryDto>>(
            Arg.Is<GetInquiryByIdQuery>(q => q.InquiryId == id),
            Arg.Any<CancellationToken>());
    }

    #endregion

    #region UpdateStatus

    [Fact]
    public async Task UpdateStatus_WithValidStatus_ReturnsOk()
    {
        Guid id = Guid.NewGuid();
        InquiryDto dto = CreateDto(id, "Reviewed");
        _bus.InvokeAsync<Result<InquiryDto>>(Arg.Any<UpdateInquiryStatusCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(dto));

        UpdateInquiryStatusRequest request = new("Reviewed");

        IActionResult result = await _controller.UpdateStatus(id, request, CancellationToken.None);

        OkObjectResult ok = result.Should().BeOfType<OkObjectResult>().Subject;
        InquiryResponse response = ok.Value.Should().BeOfType<InquiryResponse>().Subject;
        response.Status.Should().Be("Reviewed");
    }

    [Fact]
    public async Task UpdateStatus_WithInvalidStatus_Returns400()
    {
        Guid id = Guid.NewGuid();
        UpdateInquiryStatusRequest request = new("NotAValidStatus");

        IActionResult result = await _controller.UpdateStatus(id, request, CancellationToken.None);

        result.Should().BeAssignableTo<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        await _bus.DidNotReceive().InvokeAsync<Result<InquiryDto>>(
            Arg.Any<UpdateInquiryStatusCommand>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateStatus_WhenNotFound_Returns404()
    {
        Guid id = Guid.NewGuid();
        _bus.InvokeAsync<Result<InquiryDto>>(Arg.Any<UpdateInquiryStatusCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<InquiryDto>(Error.NotFound("Inquiry", id)));

        UpdateInquiryStatusRequest request = new("Reviewed");

        IActionResult result = await _controller.UpdateStatus(id, request, CancellationToken.None);

        ObjectResult objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task UpdateStatus_PassesCorrectCommandFields()
    {
        Guid id = Guid.NewGuid();
        InquiryDto dto = CreateDto(id, "Reviewed");
        _bus.InvokeAsync<Result<InquiryDto>>(Arg.Any<UpdateInquiryStatusCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(dto));

        UpdateInquiryStatusRequest request = new("Reviewed");

        await _controller.UpdateStatus(id, request, CancellationToken.None);

        await _bus.Received(1).InvokeAsync<Result<InquiryDto>>(
            Arg.Is<UpdateInquiryStatusCommand>(c =>
                c.InquiryId == id &&
                c.NewStatus == InquiryStatus.Reviewed),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateStatus_IsCaseInsensitive()
    {
        Guid id = Guid.NewGuid();
        InquiryDto dto = CreateDto(id, "Reviewed");
        _bus.InvokeAsync<Result<InquiryDto>>(Arg.Any<UpdateInquiryStatusCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(dto));

        UpdateInquiryStatusRequest request = new("reviewed");

        IActionResult result = await _controller.UpdateStatus(id, request, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }

    #endregion

    #region AddComment

    [Fact]
    public async Task AddComment_WithValidRequest_ReturnsCreated()
    {
        Guid inquiryId = Guid.NewGuid();
        Guid commentId = Guid.NewGuid();
        SetUserWithClaims(sub: "user-1", name: "Test User");

        _bus.InvokeAsync<Result<InquiryCommentId>>(Arg.Any<AddInquiryCommentCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(InquiryCommentId.Create(commentId)));

        AddInquiryCommentRequest request = new("Great progress!", false);

        IActionResult result = await _controller.AddComment(inquiryId, request, CancellationToken.None);

        CreatedResult created = result.Should().BeOfType<CreatedResult>().Subject;
        created.Location.Should().Contain(commentId.ToString());
    }

    [Fact]
    public async Task AddComment_WhenFailure_ReturnsError()
    {
        Guid inquiryId = Guid.NewGuid();
        SetUserWithClaims(sub: "user-1", name: "Test User");

        _bus.InvokeAsync<Result<InquiryCommentId>>(Arg.Any<AddInquiryCommentCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<InquiryCommentId>(Error.NotFound("Inquiry", inquiryId)));

        AddInquiryCommentRequest request = new("Comment text", false);

        IActionResult result = await _controller.AddComment(inquiryId, request, CancellationToken.None);

        ObjectResult objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task AddComment_PassesCorrectCommandFields()
    {
        Guid inquiryId = Guid.NewGuid();
        Guid tenantId = _tenantContext.TenantId.Value;
        SetUserWithClaims(sub: "author-123", name: "Jane Doe");

        _bus.InvokeAsync<Result<InquiryCommentId>>(Arg.Any<AddInquiryCommentCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(InquiryCommentId.New()));

        AddInquiryCommentRequest request = new("Internal note", true);

        await _controller.AddComment(inquiryId, request, CancellationToken.None);

        await _bus.Received(1).InvokeAsync<Result<InquiryCommentId>>(
            Arg.Is<AddInquiryCommentCommand>(c =>
                c.InquiryId == InquiryId.Create(inquiryId) &&
                c.AuthorId == "author-123" &&
                c.AuthorName == "Jane Doe" &&
                c.Content == "Internal note" &&
                c.IsInternal &&
                c.TenantId == tenantId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AddComment_WithNoNameClaim_UsesPreferredUsername()
    {
        Guid inquiryId = Guid.NewGuid();
        List<Claim> claims =
        [
            new Claim("sub", "user-1"),
            new Claim("preferred_username", "jdoe")
        ];
        _controller.ControllerContext.HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"))
        };

        _bus.InvokeAsync<Result<InquiryCommentId>>(Arg.Any<AddInquiryCommentCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(InquiryCommentId.New()));

        AddInquiryCommentRequest request = new("Comment", false);

        await _controller.AddComment(inquiryId, request, CancellationToken.None);

        await _bus.Received(1).InvokeAsync<Result<InquiryCommentId>>(
            Arg.Is<AddInquiryCommentCommand>(c => c.AuthorName == "jdoe"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AddComment_WithNoNameOrUsername_UsesUnknown()
    {
        Guid inquiryId = Guid.NewGuid();
        SetUserWithClaims(sub: "user-1");

        _bus.InvokeAsync<Result<InquiryCommentId>>(Arg.Any<AddInquiryCommentCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(InquiryCommentId.New()));

        AddInquiryCommentRequest request = new("Comment", false);

        await _controller.AddComment(inquiryId, request, CancellationToken.None);

        await _bus.Received(1).InvokeAsync<Result<InquiryCommentId>>(
            Arg.Is<AddInquiryCommentCommand>(c => c.AuthorName == "Unknown"),
            Arg.Any<CancellationToken>());
    }

    #endregion

    #region GetComments

    [Fact]
    public async Task GetComments_WithReadPermission_ReturnsAllComments()
    {
        Guid inquiryId = Guid.NewGuid();
        SetUserWithClaims(sub: "admin", permissions: "InquiriesRead");

        List<InquiryCommentDto> comments =
        [
            new InquiryCommentDto(Guid.NewGuid(), inquiryId, "author-1", "Author One", "Public comment", false, DateTimeOffset.UtcNow),
            new InquiryCommentDto(Guid.NewGuid(), inquiryId, "author-2", "Author Two", "Internal note", true, DateTimeOffset.UtcNow)
        ];

        _bus.InvokeAsync<IReadOnlyList<InquiryCommentDto>>(Arg.Any<GetInquiryCommentsQuery>(), Arg.Any<CancellationToken>())
            .Returns(comments);

        IActionResult result = await _controller.GetComments(inquiryId, CancellationToken.None);

        OkObjectResult ok = result.Should().BeOfType<OkObjectResult>().Subject;
        IReadOnlyList<InquiryCommentResponse> responses = ok.Value.Should().BeAssignableTo<IReadOnlyList<InquiryCommentResponse>>().Subject;
        responses.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetComments_WithReadPermission_IncludesInternalComments()
    {
        Guid inquiryId = Guid.NewGuid();
        SetUserWithClaims(sub: "admin", permissions: "InquiriesRead");

        _bus.InvokeAsync<IReadOnlyList<InquiryCommentDto>>(Arg.Any<GetInquiryCommentsQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<InquiryCommentDto>());

        await _controller.GetComments(inquiryId, CancellationToken.None);

        await _bus.Received(1).InvokeAsync<IReadOnlyList<InquiryCommentDto>>(
            Arg.Is<GetInquiryCommentsQuery>(q =>
                q.InquiryId == InquiryId.Create(inquiryId) &&
                q.IncludeInternal),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetComments_MapsResponseFieldsCorrectly()
    {
        Guid inquiryId = Guid.NewGuid();
        Guid commentId = Guid.NewGuid();
        DateTimeOffset createdAt = DateTimeOffset.UtcNow;
        SetUserWithClaims(sub: "admin", permissions: "InquiriesRead");

        List<InquiryCommentDto> comments =
        [
            new InquiryCommentDto(commentId, inquiryId, "auth-1", "Author", "Content", true, createdAt)
        ];

        _bus.InvokeAsync<IReadOnlyList<InquiryCommentDto>>(Arg.Any<GetInquiryCommentsQuery>(), Arg.Any<CancellationToken>())
            .Returns(comments);

        IActionResult result = await _controller.GetComments(inquiryId, CancellationToken.None);

        OkObjectResult ok = result.Should().BeOfType<OkObjectResult>().Subject;
        IReadOnlyList<InquiryCommentResponse> responses = ok.Value.Should().BeAssignableTo<IReadOnlyList<InquiryCommentResponse>>().Subject;
        InquiryCommentResponse response = responses[0];
        response.Id.Should().Be(commentId);
        response.InquiryId.Should().Be(inquiryId);
        response.AuthorId.Should().Be("auth-1");
        response.AuthorName.Should().Be("Author");
        response.Content.Should().Be("Content");
        response.IsInternal.Should().BeTrue();
        response.CreatedAt.Should().Be(createdAt.UtcDateTime);
    }

    #endregion
}
