using Asp.Versioning;
using Foundry.Billing.Api.Contracts.Payments;
using Foundry.Shared.Api.Extensions;
using Foundry.Billing.Application.Commands.ProcessPayment;
using Foundry.Billing.Application.DTOs;
using Foundry.Billing.Application.Queries.GetPaymentById;
using Foundry.Billing.Application.Queries.GetPaymentsByInvoiceId;
using Foundry.Shared.Kernel.Results;
using Foundry.Shared.Kernel.Identity.Authorization;
using Foundry.Shared.Kernel.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace Foundry.Billing.Api.Controllers;

[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/billing/payments")]
[Authorize]
[Tags("Payments")]
[Produces("application/json")]
[Consumes("application/json")]
public class PaymentsController : ControllerBase
{
    private readonly IMessageBus _bus;
    private readonly ICurrentUserService _currentUserService;

    public PaymentsController(IMessageBus bus, ICurrentUserService currentUserService)
    {
        _bus = bus;
        _currentUserService = currentUserService;
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
        Result<PaymentDto> result = await _bus.InvokeAsync<Result<PaymentDto>>(
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
        Result<IReadOnlyList<PaymentDto>> result = await _bus.InvokeAsync<Result<IReadOnlyList<PaymentDto>>>(
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
        Guid? currentUserId = _currentUserService.GetCurrentUserId();
        if (currentUserId is null)
        {
            return Unauthorized();
        }

        ProcessPaymentCommand command = new ProcessPaymentCommand(
            invoiceId,
            currentUserId.Value,
            request.Amount,
            request.Currency,
            request.PaymentMethod);

        Result<PaymentDto> result = await _bus.InvokeAsync<Result<PaymentDto>>(command, cancellationToken);

        return result.Map(ToPaymentResponse)
            .ToCreatedResult($"/api/billing/payments/{result.Value?.Id}");
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
