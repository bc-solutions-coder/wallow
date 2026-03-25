using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Wallow.Billing.Api.Contracts.Payments;
using Wallow.Billing.Application.Commands.ProcessPayment;
using Wallow.Billing.Application.DTOs;
using Wallow.Billing.Application.Queries.GetAllPayments;
using Wallow.Billing.Application.Queries.GetPaymentById;
using Wallow.Billing.Application.Queries.GetPaymentsByInvoiceId;
using Wallow.Shared.Api.Extensions;
using Wallow.Shared.Kernel.Identity.Authorization;
using Wallow.Shared.Kernel.Pagination;
using Wallow.Shared.Kernel.Results;
using Wallow.Shared.Kernel.Services;
using Wolverine;

namespace Wallow.Billing.Api.Controllers;

[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/billing/payments")]
[Authorize]
[Tags("Payments")]
[Produces("application/json")]
[Consumes("application/json")]
public class PaymentsController(IMessageBus bus, ICurrentUserService currentUserService) : ControllerBase
{

    /// <summary>
    /// Get all payments.
    /// </summary>
    [HttpGet]
    [HasPermission(PermissionType.PaymentsRead)]
    [ProducesResponseType(typeof(PagedResult<PaymentResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50,
        CancellationToken cancellationToken = default)
    {
        Result<PagedResult<PaymentDto>> result = await bus.InvokeAsync<Result<PagedResult<PaymentDto>>>(
            new GetAllPaymentsQuery(skip, take), cancellationToken);

        return result.Map(paged => new PagedResult<PaymentResponse>(
            paged.Items.Select(ToPaymentResponse).ToList(),
            paged.TotalCount,
            paged.Page,
            paged.PageSize))
            .ToActionResult();
    }

    /// <summary>
    /// Get a specific payment by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [HasPermission(PermissionType.PaymentsRead)]
    [ProducesResponseType(typeof(PaymentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        Result<PaymentDto> result = await bus.InvokeAsync<Result<PaymentDto>>(
            new GetPaymentByIdQuery(id), cancellationToken);

        return result.Map(ToPaymentResponse).ToActionResult();
    }

    /// <summary>
    /// Get all payments for a specific invoice.
    /// </summary>
    [HttpGet("invoice/{invoiceId:guid}")]
    [HasPermission(PermissionType.PaymentsRead)]
    [ProducesResponseType(typeof(IReadOnlyList<PaymentResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByInvoiceId(Guid invoiceId, CancellationToken cancellationToken)
    {
        Result<IReadOnlyList<PaymentDto>> result = await bus.InvokeAsync<Result<IReadOnlyList<PaymentDto>>>(
            new GetPaymentsByInvoiceIdQuery(invoiceId), cancellationToken);

        return result.Map(payments =>
            (IReadOnlyList<PaymentResponse>)payments.Select(ToPaymentResponse).ToList())
            .ToActionResult();
    }

    /// <summary>
    /// Process a payment for an invoice.
    /// </summary>
    [HttpPost("invoice/{invoiceId:guid}")]
    [HasPermission(PermissionType.PaymentsWrite)]
    [ProducesResponseType(typeof(PaymentResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ProcessPayment(
        Guid invoiceId,
        [FromBody] ProcessPaymentRequest request,
        CancellationToken cancellationToken)
    {
        Guid? currentUserId = currentUserService.GetCurrentUserId();
        if (currentUserId is null)
        {
            return Problem(statusCode: 401, title: "Unauthorized", detail: "Tenant context is required");
        }

        ProcessPaymentCommand command = new(
            invoiceId,
            currentUserId.Value,
            request.Amount,
            request.Currency,
            request.PaymentMethod);

        Result<PaymentDto> result = await bus.InvokeAsync<Result<PaymentDto>>(command, cancellationToken);

        if (!result.IsSuccess)
        {
            return result.ToActionResult();
        }

        return result.Map(ToPaymentResponse)
            .ToCreatedResult($"/api/billing/payments/{result.Value.Id}");
    }

    private static PaymentResponse ToPaymentResponse(PaymentDto dto) => new(
        dto.Id,
        dto.InvoiceId,
        dto.UserId,
        dto.Amount,
        dto.Currency,
        dto.Method,
        dto.Status,
        dto.TransactionReference,
        dto.FailureReason,
        dto.CompletedAt,
        dto.CreatedAt,
        dto.UpdatedAt);
}
