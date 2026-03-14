using Asp.Versioning;
using Foundry.Inquiries.Api.Contracts;
using Foundry.Inquiries.Application.Commands.SubmitInquiry;
using Foundry.Inquiries.Application.Commands.UpdateInquiryStatus;
using Foundry.Inquiries.Application.DTOs;
using Foundry.Inquiries.Application.Queries.GetInquiries;
using Foundry.Inquiries.Application.Queries.GetInquiryById;
using Foundry.Inquiries.Application.Queries.GetSubmittedInquiries;
using Foundry.Inquiries.Domain.Enums;
using Foundry.Shared.Api.Extensions;
using Foundry.Shared.Kernel.Identity.Authorization;
using Foundry.Shared.Kernel.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace Foundry.Inquiries.Api.Controllers;

[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/inquiries")]
[Authorize]
[Tags("Inquiries")]
[Produces("application/json")]
[Consumes("application/json")]
public class InquiriesController(IMessageBus bus) : ControllerBase
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
        Result<InquiryDto> result = await bus.InvokeAsync<Result<InquiryDto>>(
            new GetInquiryByIdQuery(id), cancellationToken);

        if (!result.IsSuccess)
        {
            return result.Map(ToInquiryResponse).ToActionResult();
        }

        bool hasReadPermission = User.Claims
            .Any(c => c.Type == "permission" && c.Value == PermissionType.InquiriesRead);

        if (!hasReadPermission)
        {
            string? submitterId = ExtractSubmitterId();
            if (submitterId is null || result.Value.SubmitterId != submitterId)
            {
                return NotFound();
            }
        }

        return result.Map(ToInquiryResponse).ToActionResult();
    }

    [HttpPut("{id:guid}/status")]
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

    private string? ExtractSubmitterId()
    {
        string? azp = User.FindFirst("azp")?.Value;
        if (azp is not null && azp.StartsWith("sa-", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return User.FindFirst("sub")?.Value;
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
        dto.CreatedAt.UtcDateTime);
}
