using Asp.Versioning;
using Foundry.Inquiries.Api.Contracts;
using Foundry.Inquiries.Application.Commands.SubmitInquiry;
using Foundry.Inquiries.Application.Commands.UpdateInquiryStatus;
using Foundry.Inquiries.Application.DTOs;
using Foundry.Inquiries.Application.Queries.GetInquiries;
using Foundry.Inquiries.Application.Queries.GetInquiryById;
using Foundry.Inquiries.Domain.Enums;
using Foundry.Shared.Api.Extensions;
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
public class InquiriesController : ControllerBase
{
    private readonly IMessageBus _bus;

    public InquiriesController(IMessageBus bus)
    {
        _bus = bus;
    }

    [HttpPost]
    [AllowAnonymous]
    [ProducesResponseType(typeof(InquiryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Submit(
        [FromBody] SubmitInquiryRequest request,
        CancellationToken cancellationToken)
    {
        string ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        SubmitInquiryCommand command = new SubmitInquiryCommand(
            request.Name,
            request.Email,
            request.Company,
            request.Subject,
            BudgetRange: string.Empty,
            Timeline: string.Empty,
            request.Message,
            ipAddress,
            HoneypotField: null);

        Result<InquiryDto> result = await _bus.InvokeAsync<Result<InquiryDto>>(command, cancellationToken);

        return result.Map(ToInquiryResponse).ToActionResult();
    }

    [HttpGet]
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

        Result<IReadOnlyList<InquiryDto>> result = await _bus.InvokeAsync<Result<IReadOnlyList<InquiryDto>>>(
            new GetInquiriesQuery(parsedStatus), cancellationToken);

        return result.Map(inquiries =>
            (IReadOnlyList<InquiryResponse>)inquiries.Select(ToInquiryResponse).ToList())
            .ToActionResult();
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(InquiryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        Result<InquiryDto> result = await _bus.InvokeAsync<Result<InquiryDto>>(
            new GetInquiryByIdQuery(id), cancellationToken);

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

        Result<InquiryDto> result = await _bus.InvokeAsync<Result<InquiryDto>>(
            new UpdateInquiryStatusCommand(id, newStatus), cancellationToken);

        return result.Map(ToInquiryResponse).ToActionResult();
    }

    private static InquiryResponse ToInquiryResponse(InquiryDto dto) => new(
        dto.Id,
        dto.Name,
        dto.Email,
        dto.Company,
        null,
        dto.ProjectType,
        dto.Message,
        dto.Status,
        dto.CreatedAt.UtcDateTime,
        dto.CreatedAt.UtcDateTime);
}
