using Asp.Versioning;
using Wallow.Billing.Api.Contracts.Subscriptions;
using Wallow.Billing.Application.Commands.CancelSubscription;
using Wallow.Billing.Application.Commands.CreateSubscription;
using Wallow.Billing.Application.DTOs;
using Wallow.Billing.Application.Queries.GetAllSubscriptions;
using Wallow.Billing.Application.Queries.GetSubscriptionById;
using Wallow.Billing.Application.Queries.GetSubscriptionsByUserId;
using Wallow.Shared.Api.Extensions;
using Wallow.Shared.Kernel.Identity.Authorization;
using Wallow.Shared.Kernel.Pagination;
using Wallow.Shared.Kernel.Results;
using Wallow.Shared.Kernel.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace Wallow.Billing.Api.Controllers;

[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/billing/subscriptions")]
[Authorize]
[Tags("Subscriptions")]
[Produces("application/json")]
[Consumes("application/json")]
public class SubscriptionsController(IMessageBus bus, ICurrentUserService currentUserService) : ControllerBase
{

    /// <summary>
    /// Get all subscriptions.
    /// </summary>
    [HttpGet]
    [HasPermission(PermissionType.SubscriptionsRead)]
    [ProducesResponseType(typeof(PagedResult<SubscriptionResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50,
        CancellationToken cancellationToken = default)
    {
        Result<PagedResult<SubscriptionDto>> result = await bus.InvokeAsync<Result<PagedResult<SubscriptionDto>>>(
            new GetAllSubscriptionsQuery(skip, take), cancellationToken);

        return result.Map(paged => new PagedResult<SubscriptionResponse>(
            paged.Items.Select(ToSubscriptionResponse).ToList(),
            paged.TotalCount,
            paged.Page,
            paged.PageSize))
            .ToActionResult();
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
        Result<SubscriptionDto> result = await bus.InvokeAsync<Result<SubscriptionDto>>(
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
        Result<IReadOnlyList<SubscriptionDto>> result = await bus.InvokeAsync<Result<IReadOnlyList<SubscriptionDto>>>(
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
        Guid? currentUserId = currentUserService.GetCurrentUserId();
        if (currentUserId is null)
        {
            return Problem(statusCode: 401, title: "Unauthorized", detail: "Tenant context is required");
        }

        CreateSubscriptionCommand command = new(
            currentUserId.Value,
            request.PlanName,
            request.Price,
            request.Currency,
            request.StartDate,
            request.PeriodEnd);

        Result<SubscriptionDto> result = await bus.InvokeAsync<Result<SubscriptionDto>>(command, cancellationToken);

        if (!result.IsSuccess)
        {
            return result.ToActionResult();
        }

        return result.Map(ToSubscriptionResponse)
            .ToCreatedResult($"/api/billing/subscriptions/{result.Value.Id}");
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
        Guid? currentUserId = currentUserService.GetCurrentUserId();
        if (currentUserId is null)
        {
            return Problem(statusCode: 401, title: "Unauthorized", detail: "Tenant context is required");
        }

        CancelSubscriptionCommand command = new(id, currentUserId.Value);

        Result result = await bus.InvokeAsync<Result>(command, cancellationToken);

        if (result.IsSuccess)
        {
            return NoContent();
        }

        return result.ToActionResult();
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
