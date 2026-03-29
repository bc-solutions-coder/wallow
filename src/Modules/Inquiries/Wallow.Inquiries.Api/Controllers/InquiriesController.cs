using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Wallow.Inquiries.Api.Contracts;
using Wallow.Inquiries.Application.Commands.AddInquiryComment;
using Wallow.Inquiries.Application.Commands.SubmitInquiry;
using Wallow.Inquiries.Application.Commands.UpdateInquiryStatus;
using Wallow.Inquiries.Application.DTOs;
using Wallow.Inquiries.Application.Queries.GetInquiries;
using Wallow.Inquiries.Application.Queries.GetInquiryById;
using Wallow.Inquiries.Application.Queries.GetInquiryComments;
using Wallow.Inquiries.Application.Queries.GetSubmittedInquiries;
using Wallow.Inquiries.Domain.Enums;
using Wallow.Inquiries.Domain.Identity;
using Wallow.Shared.Api.Extensions;
using Wallow.Shared.Kernel.Extensions;
using Wallow.Shared.Kernel.Identity.Authorization;
using Wallow.Shared.Kernel.MultiTenancy;
using Wallow.Shared.Kernel.Results;
using Wolverine;

namespace Wallow.Inquiries.Api.Controllers;

[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/inquiries")]
[Authorize]
[Tags("Inquiries")]
[Produces("application/json")]
[Consumes("application/json")]
[IgnoreAntiforgeryToken]
public partial class InquiriesController(IMessageBus bus, ITenantContext tenantContext, ILogger<InquiriesController> logger) : ControllerBase
{

    [HttpPost]
    [HasPermission(PermissionType.InquiriesWrite)]
    [ProducesResponseType(typeof(InquiryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Submit(
        [FromBody] SubmitInquiryRequest request,
        CancellationToken cancellationToken)
    {
        string? submitterId = ExtractSubmitterId();

        SubmitInquiryCommand command = new(
            request.Name,
            request.Email,
            request.Phone,
            request.Company,
            submitterId,
            request.ProjectType,
            request.BudgetRange,
            request.Timeline,
            request.Message);

        Result<InquiryDto> result = await bus.InvokeAsync<Result<InquiryDto>>(command, cancellationToken);

        if (result.IsSuccess)
        {
            LogInquiryCreated(result.Value.Id, submitterId, tenantContext.TenantId.Value);
        }

        return result.Map(ToInquiryResponse).ToActionResult();
    }

    [HttpGet]
    [HasPermission(PermissionType.InquiriesRead)]
    [ProducesResponseType(typeof(IReadOnlyList<InquiryResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? status,
        CancellationToken cancellationToken)
    {
        InquiryStatus? parsedStatus = null;
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<InquiryStatus>(status, ignoreCase: true, out InquiryStatus parsed))
        {
            parsedStatus = parsed;
        }

        Result<IReadOnlyList<InquiryDto>> result = await bus.InvokeAsync<Result<IReadOnlyList<InquiryDto>>>(
            new GetInquiriesQuery(parsedStatus), cancellationToken);

        return result.Map(inquiries =>
            (IReadOnlyList<InquiryResponse>)inquiries.Select(ToInquiryResponse).ToList())
            .ToActionResult();
    }

    [HttpGet("submitted")]
    [ProducesResponseType(typeof(IReadOnlyList<InquiryResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSubmitted(CancellationToken cancellationToken)
    {
        string? submitterId = ExtractSubmitterId();
        if (submitterId is null)
        {
            return Ok(Array.Empty<InquiryResponse>());
        }

        Result<IReadOnlyList<InquiryDto>> result = await bus.InvokeAsync<Result<IReadOnlyList<InquiryDto>>>(
            new GetSubmittedInquiriesQuery(submitterId), cancellationToken);

        return result.Map(inquiries =>
            (IReadOnlyList<InquiryResponse>)inquiries.Select(ToInquiryResponse).ToList())
            .ToActionResult();
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(InquiryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        string? userId = User.GetUserId();
        Guid currentTenantId = tenantContext.TenantId.Value;

        LogFetchingInquiry(id, userId, currentTenantId);

        Result<InquiryDto> result = await bus.InvokeAsync<Result<InquiryDto>>(
            new GetInquiryByIdQuery(id), cancellationToken);

        if (!result.IsSuccess)
        {
            LogInquiryNotFound(id, userId, currentTenantId);
            return result.Map(ToInquiryResponse).ToActionResult();
        }

        bool hasReadPermission = User.GetPermissions().Contains(PermissionType.InquiriesRead);

        if (!hasReadPermission)
        {
            string? submitterId = ExtractSubmitterId();
            if (submitterId is null || result.Value.SubmitterId != submitterId)
            {
                LogInquiryAccessDenied(id, userId, submitterId, result.Value.SubmitterId);
                return NotFound();
            }
        }

        return result.Map(ToInquiryResponse).ToActionResult();
    }

    [HttpPatch("{id:guid}/status")]
    [HasPermission(PermissionType.InquiriesWrite)]
    [ProducesResponseType(typeof(InquiryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateStatus(
        Guid id,
        [FromBody] UpdateInquiryStatusRequest request,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<InquiryStatus>(request.NewStatus, ignoreCase: true, out InquiryStatus newStatus))
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Bad Request",
                Detail = $"Invalid status value: '{request.NewStatus}'"
            });
        }

        Result<InquiryDto> result = await bus.InvokeAsync<Result<InquiryDto>>(
            new UpdateInquiryStatusCommand(id, newStatus), cancellationToken);

        return result.Map(ToInquiryResponse).ToActionResult();
    }

    [HttpPost("{id:guid}/comments")]
    [HasPermission(PermissionType.InquiriesWrite)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AddComment(
        Guid id,
        [FromBody] AddInquiryCommentRequest request,
        CancellationToken cancellationToken)
    {
        string authorId = User.GetUserId() ?? string.Empty;
        string authorName = User.GetDisplayName() ?? "Unknown";

        AddInquiryCommentCommand command = new(
            InquiryId.Create(id),
            authorId,
            authorName,
            request.Content,
            request.IsInternal);

        Result<InquiryCommentId> result = await bus.InvokeAsync<Result<InquiryCommentId>>(command, cancellationToken);

        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        return Created($"{id}/comments/{result.Value.Value}", new { Id = result.Value.Value });
    }

    [HttpGet("{id:guid}/comments")]
    [ProducesResponseType(typeof(IReadOnlyList<InquiryCommentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetComments(Guid id, CancellationToken cancellationToken)
    {
        bool hasReadPermission = User.GetPermissions().Contains(PermissionType.InquiriesRead);

        if (!hasReadPermission)
        {
            string? submitterId = ExtractSubmitterId();
            if (submitterId is null)
            {
                return NotFound();
            }

            Result<InquiryDto> inquiryResult = await bus.InvokeAsync<Result<InquiryDto>>(
                new GetInquiryByIdQuery(id), cancellationToken);

            if (!inquiryResult.IsSuccess || inquiryResult.Value.SubmitterId != submitterId)
            {
                return NotFound();
            }
        }

        bool includeInternal = hasReadPermission;
        string? userId = User.GetUserId();

        LogFetchingComments(id, userId, tenantContext.TenantId.Value, hasReadPermission, includeInternal);

        IReadOnlyList<InquiryCommentDto> comments = await bus.InvokeAsync<IReadOnlyList<InquiryCommentDto>>(
            new GetInquiryCommentsQuery(InquiryId.Create(id), includeInternal), cancellationToken);

        LogCommentsReturned(id, userId, comments.Count, includeInternal);

        IReadOnlyList<InquiryCommentResponse> response = comments
            .Select(ToInquiryCommentResponse)
            .ToList();

        return Ok(response);
    }

    private string? ExtractSubmitterId()
    {
        string? azp = User.GetClientId();
        if (azp is not null && azp.StartsWith("sa-", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return User.GetUserId();
    }

    private static InquiryResponse ToInquiryResponse(InquiryDto dto) => new(
        dto.Id,
        dto.Name,
        dto.Email,
        dto.Phone,
        dto.Company,
        dto.SubmitterId,
        dto.ProjectType,
        dto.BudgetRange,
        dto.Timeline,
        dto.Message,
        dto.Status,
        dto.CreatedAt.UtcDateTime,
        dto.UpdatedAt ?? dto.CreatedAt.UtcDateTime);

    private static InquiryCommentResponse ToInquiryCommentResponse(InquiryCommentDto dto) => new(
        dto.Id,
        dto.InquiryId,
        dto.AuthorId,
        dto.AuthorName,
        dto.Content,
        dto.IsInternal,
        dto.CreatedAt.UtcDateTime);

    [LoggerMessage(Level = LogLevel.Information, Message = "Inquiry {InquiryId} created by user {SubmitterId} under tenant {TenantId}")]
    private partial void LogInquiryCreated(Guid inquiryId, string? submitterId, Guid tenantId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Fetching inquiry {InquiryId} for user {UserId} under tenant {TenantId}")]
    private partial void LogFetchingInquiry(Guid inquiryId, string? userId, Guid tenantId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Inquiry {InquiryId} not found for user {UserId} under tenant {TenantId} — not in database (wrong tenant or does not exist)")]
    private partial void LogInquiryNotFound(Guid inquiryId, string? userId, Guid tenantId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Inquiry {InquiryId} access denied for user {UserId} — no InquiriesRead permission, submitterId {SubmitterId} does not match inquiry submitter {InquirySubmitterId}")]
    private partial void LogInquiryAccessDenied(Guid inquiryId, string? userId, string? submitterId, string? inquirySubmitterId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Fetching comments for inquiry {InquiryId} by user {UserId} under tenant {TenantId}, hasReadPermission={HasReadPermission}, includeInternal={IncludeInternal}")]
    private partial void LogFetchingComments(Guid inquiryId, string? userId, Guid tenantId, bool hasReadPermission, bool includeInternal);

    [LoggerMessage(Level = LogLevel.Information, Message = "Returning {CommentCount} comments for inquiry {InquiryId} to user {UserId}, includeInternal={IncludeInternal}")]
    private partial void LogCommentsReturned(Guid inquiryId, string? userId, int commentCount, bool includeInternal);
}
