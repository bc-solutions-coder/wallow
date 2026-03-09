using Asp.Versioning;
using Foundry.Configuration.Api.Contracts.Requests;
using Foundry.Configuration.Api.Contracts.Responses;
using Foundry.Shared.Api.Extensions;
using Foundry.Configuration.Api.Mappings;
using Foundry.Configuration.Application.FeatureFlags.Commands.CreateFeatureFlag;
using Foundry.Configuration.Application.FeatureFlags.Commands.CreateOverride;
using Foundry.Configuration.Application.FeatureFlags.Commands.DeleteFeatureFlag;
using Foundry.Configuration.Application.FeatureFlags.Commands.DeleteOverride;
using Foundry.Configuration.Application.FeatureFlags.Commands.UpdateFeatureFlag;
using Foundry.Configuration.Application.FeatureFlags.Contracts;
using Foundry.Configuration.Application.FeatureFlags.DTOs;
using Foundry.Configuration.Application.FeatureFlags.Queries.EvaluateFlag;
using Foundry.Configuration.Application.FeatureFlags.Queries.GetAllFlags;
using Foundry.Configuration.Application.FeatureFlags.Queries.GetOverridesForFlag;
using Foundry.Shared.Kernel.Identity.Authorization;
using Foundry.Shared.Kernel.MultiTenancy;
using Foundry.Shared.Kernel.Services;
using Foundry.Shared.Kernel.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace Foundry.Configuration.Api.Controllers;

[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/configuration/feature-flags")]
[Authorize]
[Tags("Configuration")]
[Produces("application/json")]
[Consumes("application/json")]
public class FeatureFlagsController(
    IMessageBus bus,
    ITenantContext tenantContext,
    IFeatureFlagService featureFlagService,
    ICurrentUserService currentUserService) : ControllerBase
{

    /// <summary>
    /// Get all feature flags (admin only).
    /// </summary>
    [HttpGet]
    [HasPermission(PermissionType.ConfigurationManage)]
    [ProducesResponseType(typeof(IReadOnlyList<FeatureFlagResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        Result<IReadOnlyList<FeatureFlagDto>> result = await bus.InvokeAsync<Result<IReadOnlyList<FeatureFlagDto>>>(
            new GetAllFlagsQuery(), cancellationToken);

        return result.Map(flags =>
            (IReadOnlyList<FeatureFlagResponse>)flags.Select(ToFeatureFlagResponse).ToList())
            .ToActionResult();
    }

    /// <summary>
    /// Create a new feature flag (admin only).
    /// </summary>
    [HttpPost]
    [HasPermission(PermissionType.ConfigurationManage)]
    [ProducesResponseType(typeof(FeatureFlagResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        [FromBody] CreateFeatureFlagRequest request,
        CancellationToken cancellationToken)
    {
        CreateFeatureFlagCommand command = new(
            request.Key,
            request.Name,
            request.Description,
            request.FlagType.ToDomain(),
            request.DefaultEnabled,
            request.RolloutPercentage,
            request.Variants,
            request.DefaultVariant);

        Result<FeatureFlagDto> result = await bus.InvokeAsync<Result<FeatureFlagDto>>(command, cancellationToken);

        if (!result.IsSuccess)
        {
            return result.ToActionResult();
        }

        return result.Map(ToFeatureFlagResponse)
            .ToCreatedResult($"/api/configuration/feature-flags/{result.Value.Id}");
    }

    /// <summary>
    /// Update an existing feature flag (admin only).
    /// </summary>
    [HttpPut("{id:guid}")]
    [HasPermission(PermissionType.ConfigurationManage)]
    [ProducesResponseType(typeof(FeatureFlagResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateFeatureFlagRequest request,
        CancellationToken cancellationToken)
    {
        UpdateFeatureFlagCommand command = new(
            id,
            request.Name,
            request.Description,
            request.DefaultEnabled,
            request.RolloutPercentage);

        Result<FeatureFlagDto> result = await bus.InvokeAsync<Result<FeatureFlagDto>>(command, cancellationToken);

        return result.Map(ToFeatureFlagResponse).ToActionResult();
    }

    /// <summary>
    /// Delete a feature flag (admin only).
    /// </summary>
    [HttpDelete("{id:guid}")]
    [HasPermission(PermissionType.ConfigurationManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        DeleteFeatureFlagCommand command = new(id);

        Result result = await bus.InvokeAsync<Result>(command, cancellationToken);

        if (result.IsSuccess)
        {
            return NoContent();
        }

        return result.ToActionResult();
    }

    /// <summary>
    /// Get all overrides for a specific flag (admin only).
    /// </summary>
    [HttpGet("{id:guid}/overrides")]
    [HasPermission(PermissionType.ConfigurationManage)]
    [ProducesResponseType(typeof(IReadOnlyList<FeatureFlagOverrideResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetOverrides(Guid id, CancellationToken cancellationToken)
    {
        Result<IReadOnlyList<FeatureFlagOverrideDto>> result = await bus.InvokeAsync<Result<IReadOnlyList<FeatureFlagOverrideDto>>>(
            new GetOverridesForFlagQuery(id), cancellationToken);

        return result.Map(overrides =>
            (IReadOnlyList<FeatureFlagOverrideResponse>)overrides.Select(ToFeatureFlagOverrideResponse).ToList())
            .ToActionResult();
    }

    /// <summary>
    /// Create an override for a feature flag (admin only).
    /// </summary>
    [HttpPost("{id:guid}/overrides")]
    [HasPermission(PermissionType.ConfigurationManage)]
    [ProducesResponseType(typeof(FeatureFlagOverrideResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateOverride(
        Guid id,
        [FromBody] CreateOverrideRequest request,
        CancellationToken cancellationToken)
    {
        CreateOverrideCommand command = new(
            id,
            request.TenantId,
            request.UserId,
            request.IsEnabled,
            request.Variant,
            request.ExpiresAt);

        Result<FeatureFlagOverrideDto> result = await bus.InvokeAsync<Result<FeatureFlagOverrideDto>>(command, cancellationToken);

        if (!result.IsSuccess)
        {
            return result.ToActionResult();
        }

        return result.Map(ToFeatureFlagOverrideResponse)
            .ToCreatedResult($"/api/configuration/feature-flags/{id}/overrides/{result.Value.Id}");
    }

    /// <summary>
    /// Delete an override (admin only).
    /// </summary>
    [HttpDelete("{flagId:guid}/overrides/{overrideId:guid}")]
    [HasPermission(PermissionType.ConfigurationManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DeleteOverride(
        Guid _,
        Guid overrideId,
        CancellationToken cancellationToken)
    {
        DeleteOverrideCommand command = new(overrideId);

        Result result = await bus.InvokeAsync<Result>(command, cancellationToken);

        if (result.IsSuccess)
        {
            return NoContent();
        }

        return result.ToActionResult();
    }

    /// <summary>
    /// Evaluate a single feature flag by key for the current tenant/user context.
    /// </summary>
    [HttpGet("{key}/evaluate")]
    [HasPermission(PermissionType.ConfigurationRead)]
    [ProducesResponseType(typeof(FlagEvaluationResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> EvaluateByKey(string key, CancellationToken cancellationToken)
    {
        Guid tenantId = tenantContext.TenantId.Value;
        Guid? userId = currentUserService.GetCurrentUserId();

        EvaluateFlagQuery query = new(key, tenantId, userId);
        Result<FlagEvaluationResultDto> result = await bus.InvokeAsync<Result<FlagEvaluationResultDto>>(query, cancellationToken);

        return result.Map(dto => new FlagEvaluationResponse(dto.Key, dto.IsEnabled, dto.Variant))
            .ToActionResult();
    }

    /// <summary>
    /// Get all feature flags evaluated for the current tenant/user context.
    /// Any authenticated user can call this endpoint.
    /// </summary>
    [HttpGet("evaluate")]
    [ProducesResponseType(typeof(Dictionary<string, object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Evaluate(CancellationToken cancellationToken)
    {
        Guid tenantId = tenantContext.TenantId.Value;
        Guid? userId = currentUserService.GetCurrentUserId();

        Dictionary<string, object> flags = await featureFlagService.GetAllFlagsAsync(tenantId, userId, cancellationToken);

        return Ok(flags);
    }

    private static FeatureFlagResponse ToFeatureFlagResponse(FeatureFlagDto dto) => new(
        dto.Id,
        dto.Key,
        dto.Name,
        dto.Description,
        dto.FlagType.ToApi(),
        dto.DefaultEnabled,
        dto.RolloutPercentage,
        dto.Variants,
        dto.DefaultVariant,
        dto.CreatedAt,
        dto.UpdatedAt);

    private static FeatureFlagOverrideResponse ToFeatureFlagOverrideResponse(FeatureFlagOverrideDto dto) => new(
        dto.Id,
        dto.FlagId,
        dto.TenantId,
        dto.UserId,
        dto.IsEnabled,
        dto.Variant,
        dto.ExpiresAt,
        dto.CreatedAt);
}
