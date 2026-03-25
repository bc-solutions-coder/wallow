using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Wallow.Billing.Api.Contracts.Invoices;
using Wallow.Billing.Application.Commands.AddLineItem;
using Wallow.Billing.Application.Commands.CancelInvoice;
using Wallow.Billing.Application.Commands.CreateInvoice;
using Wallow.Billing.Application.Commands.IssueInvoice;
using Wallow.Billing.Application.DTOs;
using Wallow.Billing.Application.Queries.GetAllInvoices;
using Wallow.Billing.Application.Queries.GetInvoiceById;
using Wallow.Billing.Application.Queries.GetInvoicesByUserId;
using Wallow.Shared.Api.Extensions;
using Wallow.Shared.Kernel.Identity.Authorization;
using Wallow.Shared.Kernel.Pagination;
using Wallow.Shared.Kernel.Results;
using Wallow.Shared.Kernel.Services;
using Wolverine;

namespace Wallow.Billing.Api.Controllers;

[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/billing/invoices")]
[Authorize]
[Tags("Invoices")]
[Produces("application/json")]
[Consumes("application/json")]
public class InvoicesController(IMessageBus bus, ICurrentUserService currentUserService) : ControllerBase
{

    /// <summary>
    /// Get all invoices.
    /// </summary>
    [HttpGet]
    [HasPermission(PermissionType.InvoicesRead)]
    [ProducesResponseType(typeof(PagedResult<InvoiceResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50,
        CancellationToken cancellationToken = default)
    {
        Result<PagedResult<InvoiceDto>> result = await bus.InvokeAsync<Result<PagedResult<InvoiceDto>>>(
            new GetAllInvoicesQuery(skip, take), cancellationToken);

        return result.Map(paged => new PagedResult<InvoiceResponse>(
            paged.Items.Select(ToInvoiceResponse).ToList(),
            paged.TotalCount,
            paged.Page,
            paged.PageSize))
            .ToActionResult();
    }

    /// <summary>
    /// Get a specific invoice by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [HasPermission(PermissionType.InvoicesRead)]
    [ProducesResponseType(typeof(InvoiceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        Result<InvoiceDto> result = await bus.InvokeAsync<Result<InvoiceDto>>(
            new GetInvoiceByIdQuery(id), cancellationToken);

        return result.Map(ToInvoiceResponse).ToActionResult();
    }

    /// <summary>
    /// Get all invoices for a specific user.
    /// </summary>
    [HttpGet("user/{userId:guid}")]
    [HasPermission(PermissionType.InvoicesRead)]
    [ProducesResponseType(typeof(IReadOnlyList<InvoiceResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByUserId(Guid userId, CancellationToken cancellationToken)
    {
        Result<IReadOnlyList<InvoiceDto>> result = await bus.InvokeAsync<Result<IReadOnlyList<InvoiceDto>>>(
            new GetInvoicesByUserIdQuery(userId), cancellationToken);

        return result.Map(invoices =>
            (IReadOnlyList<InvoiceResponse>)invoices.Select(ToInvoiceResponse).ToList())
            .ToActionResult();
    }

    /// <summary>
    /// Create a new invoice.
    /// </summary>
    [HttpPost]
    [HasPermission(PermissionType.InvoicesWrite)]
    [ProducesResponseType(typeof(InvoiceResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        [FromBody] CreateInvoiceRequest request,
        CancellationToken cancellationToken)
    {
        Guid? currentUserId = currentUserService.GetCurrentUserId();
        if (currentUserId is null)
        {
            return Problem(statusCode: 401, title: "Unauthorized", detail: "Tenant context is required");
        }

        Guid targetUserId = request.UserId is not null && User.IsInRole("admin")
            ? request.UserId.Value
            : currentUserId.Value;

        CreateInvoiceCommand command = new(
            targetUserId,
            request.InvoiceNumber,
            request.Currency,
            request.DueDate);

        Result<InvoiceDto> result = await bus.InvokeAsync<Result<InvoiceDto>>(command, cancellationToken);

        if (!result.IsSuccess)
        {
            return result.ToActionResult();
        }

        return result.Map(ToInvoiceResponse)
            .ToCreatedResult($"/api/billing/invoices/{result.Value.Id}");
    }

    /// <summary>
    /// Add a line item to an invoice.
    /// </summary>
    [HttpPost("{id:guid}/line-items")]
    [HasPermission(PermissionType.InvoicesWrite)]
    [ProducesResponseType(typeof(InvoiceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AddLineItem(
        Guid id,
        [FromBody] AddLineItemRequest request,
        CancellationToken cancellationToken)
    {
        Guid? currentUserId = currentUserService.GetCurrentUserId();
        if (currentUserId is null)
        {
            return Problem(statusCode: 401, title: "Unauthorized", detail: "Tenant context is required");
        }

        AddLineItemCommand command = new(
            id,
            request.Description,
            request.UnitPrice,
            request.Quantity,
            currentUserId.Value);

        Result<InvoiceDto> result = await bus.InvokeAsync<Result<InvoiceDto>>(command, cancellationToken);

        return result.Map(ToInvoiceResponse).ToActionResult();
    }

    /// <summary>
    /// Issue an invoice to make it active.
    /// </summary>
    [HttpPost("{id:guid}/issue")]
    [HasPermission(PermissionType.InvoicesWrite)]
    [ProducesResponseType(typeof(InvoiceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Issue(Guid id, CancellationToken cancellationToken)
    {
        Guid? currentUserId = currentUserService.GetCurrentUserId();
        if (currentUserId is null)
        {
            return Problem(statusCode: 401, title: "Unauthorized", detail: "Tenant context is required");
        }

        IssueInvoiceCommand command = new(id, currentUserId.Value);

        Result<InvoiceDto> result = await bus.InvokeAsync<Result<InvoiceDto>>(command, cancellationToken);

        return result.Map(ToInvoiceResponse).ToActionResult();
    }

    /// <summary>
    /// Cancel an invoice.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [HasPermission(PermissionType.InvoicesWrite)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken cancellationToken)
    {
        Guid? currentUserId = currentUserService.GetCurrentUserId();
        if (currentUserId is null)
        {
            return Problem(statusCode: 401, title: "Unauthorized", detail: "Tenant context is required");
        }

        CancelInvoiceCommand command = new(id, currentUserId.Value);

        Result result = await bus.InvokeAsync<Result>(command, cancellationToken);

        if (result.IsSuccess)
        {
            return NoContent();
        }

        return result.ToActionResult();
    }

    private static InvoiceResponse ToInvoiceResponse(InvoiceDto dto) => new(
        dto.Id,
        dto.UserId,
        dto.InvoiceNumber,
        dto.Status,
        dto.TotalAmount,
        dto.Currency,
        dto.DueDate,
        dto.PaidAt,
        dto.CreatedAt,
        dto.UpdatedAt,
        dto.LineItems.Select(ToLineItemResponse).ToList());

    private static InvoiceLineItemResponse ToLineItemResponse(InvoiceLineItemDto dto) => new(
        dto.Id,
        dto.Description,
        dto.UnitPrice,
        dto.Currency,
        dto.Quantity,
        dto.LineTotal);
}
