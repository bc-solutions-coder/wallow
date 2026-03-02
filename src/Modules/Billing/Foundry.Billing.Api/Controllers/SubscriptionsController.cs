using Asp.Versioning;
using Foundry.Billing.Api.Contracts.Subscriptions;
using Foundry.Billing.Api.Extensions;
using Foundry.Billing.Application.Commands.CancelSubscription;
using Foundry.Billing.Application.Commands.CreateSubscription;
using Foundry.Billing.Application.DTOs;
using Foundry.Billing.Application.Queries.GetSubscriptionById;
using Foundry.Billing.Application.Queries.GetSubscriptionsByUserId;
using Foundry.Shared.Kernel.Results;
using Foundry.Shared.Kernel.Identity.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace Foundry.Billing.Api.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/billing/subscriptions")]
[Authorize]
[Tags("Subscriptions")]
[Produces("application/json")]
[Consumes("application/json")]
public class SubscriptionsController : ControllerBase
{
    private readonly IMessageBus _bus;

    public SubscriptionsController(IMessageBus bus)
    {
        _bus = bus;
    }

    /// <summary>
    /// Get a specific subscription by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [HasPermission(PermissionType.SubscriptionsRead)]
    [ProducesResponseType(typeof(SubscriptionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        Result<SubscriptionDto> result = await _bus.InvokeAsync<Result<SubscriptionDto>>(
            new GetSubscriptionByIdQuery(id), cancellationToken);

        return result.Map(ToSubscriptionResponse).ToActionResult();
    }

    /// <summary>
    /// Get all subscriptions for a specific user.
    /// </summary>
    [HttpGet("user/{userId:guid}")]
    [HasPermission(PermissionType.SubscriptionsRead)]
    [ProducesResponseType(typeof(IReadOnlyList<SubscriptionResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByUserId(Guid userId, CancellationToken cancellationToken)
    {
        Result<IReadOnlyList<SubscriptionDto>> result = await _bus.InvokeAsync<Result<IReadOnlyList<SubscriptionDto>>>(
            new GetSubscriptionsByUserIdQuery(userId), cancellationToken);

        return result.Map(subscriptions =>
            (IReadOnlyList<SubscriptionResponse>)subscriptions.Select(ToSubscriptionResponse).ToList())
            .ToActionResult();
    }

    /// <summary>
    /// Create a new subscription.
    /// </summary>
    [HttpPost]
    [HasPermission(PermissionType.SubscriptionsWrite)]
    [ProducesResponseType(typeof(SubscriptionResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        [FromBody] CreateSubscriptionRequest request,
        CancellationToken cancellationToken)
    {
        Guid userId = GetCurrentUserId();

        CreateSubscriptionCommand command = new CreateSubscriptionCommand(
            userId,
            request.PlanName,
            request.Price,
            request.Currency,
            request.StartDate,
            request.PeriodEnd);

        Result<SubscriptionDto> result = await _bus.InvokeAsync<Result<SubscriptionDto>>(command, cancellationToken);

        return result.Map(ToSubscriptionResponse)
            .ToCreatedResult($"/api/billing/subscriptions/{result.Value?.Id}");
    }

    /// <summary>
    /// Cancel a subscription.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [HasPermission(PermissionType.SubscriptionsWrite)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken cancellationToken)
    {
        Guid userId = GetCurrentUserId();

        CancelSubscriptionCommand command = new CancelSubscriptionCommand(id, userId);

        Result result = await _bus.InvokeAsync<Result>(command, cancellationToken);

        if (result.IsSuccess)
        {
            return NoContent();
        }

        return result.ToActionResult();
    }

    private Guid GetCurrentUserId()
    {
        string? userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;

        if (userIdClaim is not null && Guid.TryParse(userIdClaim, out Guid userId))
        {
            return userId;
        }

        return Guid.Empty;
    }

    private static SubscriptionResponse ToSubscriptionResponse(SubscriptionDto dto) => new(
        dto.Id,
        dto.UserId,
        dto.PlanName,
        dto.Price,
        dto.Currency,
        dto.Status,
        dto.StartDate,
        dto.EndDate,
        dto.CurrentPeriodStart,
        dto.CurrentPeriodEnd,
        dto.CancelledAt,
        dto.CreatedAt,
        dto.UpdatedAt);
}
