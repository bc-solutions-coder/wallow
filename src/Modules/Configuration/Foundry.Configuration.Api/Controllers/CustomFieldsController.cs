using Asp.Versioning;
using Foundry.Configuration.Application.Commands.CreateCustomFieldDefinition;
using Foundry.Configuration.Application.Commands.DeactivateCustomFieldDefinition;
using Foundry.Configuration.Application.Commands.ReorderCustomFields;
using Foundry.Configuration.Application.Commands.UpdateCustomFieldDefinition;
using Foundry.Configuration.Application.Contracts.DTOs;
using Foundry.Configuration.Application.Queries;
using Foundry.Shared.Api.Extensions;
using Foundry.Shared.Kernel.CustomFields;
using Foundry.Shared.Kernel.Identity.Authorization;
using Foundry.Shared.Kernel.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace Foundry.Configuration.Api.Controllers;

[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/configuration/custom-fields")]
[Authorize]
[HasPermission(PermissionType.ConfigurationManage)]
[Tags("Configuration")]
[Produces("application/json")]
[Consumes("application/json")]
public class CustomFieldsController(IMessageBus bus) : ControllerBase
{

    /// <summary>
    /// Get all entity types that support custom fields.
    /// </summary>
    [HttpGet("entity-types")]
    [ProducesResponseType(typeof(IReadOnlyList<EntityTypeDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<EntityTypeDto>>> GetEntityTypes(
        CancellationToken cancellationToken)
    {
        IReadOnlyList<EntityTypeDto> result = await bus.InvokeAsync<IReadOnlyList<EntityTypeDto>>(
            new GetSupportedEntityTypes(),
            cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Get all custom field definitions for an entity type.
    /// </summary>
    /// <param name="entityType">The entity type (e.g., "Invoice", "Payment")</param>
    /// <param name="includeInactive">Include deactivated fields</param>
    /// <param name="cancellationToken">Cancellation token</param>
    [HttpGet("{entityType}")]
    [ProducesResponseType(typeof(IReadOnlyList<CustomFieldDefinitionDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CustomFieldDefinitionDto>>> GetByEntityType(
        string entityType,
        [FromQuery] bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<CustomFieldDefinitionDto> result = await bus.InvokeAsync<IReadOnlyList<CustomFieldDefinitionDto>>(
            new GetCustomFieldDefinitions(entityType, includeInactive),
            cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Get a specific custom field definition by ID.
    /// </summary>
    [HttpGet("by-id/{id:guid}")]
    [ProducesResponseType(typeof(CustomFieldDefinitionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CustomFieldDefinitionDto>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        CustomFieldDefinitionDto? result = await bus.InvokeAsync<CustomFieldDefinitionDto?>(
            new GetCustomFieldDefinitionById(id),
            cancellationToken);

        if (result == null)
        {
            return NotFound();
        }

        return Ok(result);
    }

    /// <summary>
    /// Create a new custom field definition.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(CustomFieldDefinitionDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        CreateCustomFieldRequest request,
        CancellationToken cancellationToken)
    {
        CreateCustomFieldDefinitionCommand command = new(
            request.EntityType,
            request.FieldKey,
            request.DisplayName,
            request.FieldType,
            request.Description,
            request.IsRequired,
            request.ValidationRules,
            request.Options);

        Result<CustomFieldDefinitionDto> result = await bus.InvokeAsync<Result<CustomFieldDefinitionDto>>(command, cancellationToken);

        return result.ToCreatedResult(
            nameof(GetById),
            "CustomFields",
            dto => new { id = dto.Id });
    }

    /// <summary>
    /// Update a custom field definition.
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(CustomFieldDefinitionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CustomFieldDefinitionDto>> Update(
        Guid id,
        UpdateCustomFieldRequest request,
        CancellationToken cancellationToken)
    {
        UpdateCustomFieldDefinitionCommand command = new(
            id,
            request.DisplayName,
            request.Description,
            request.ClearDescription,
            request.IsRequired,
            request.DisplayOrder,
            request.ValidationRules,
            request.Options);

        CustomFieldDefinitionDto result = await bus.InvokeAsync<CustomFieldDefinitionDto>(command, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Deactivate a custom field (soft delete - preserves existing data).
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Deactivate(
        Guid id,
        CancellationToken cancellationToken)
    {
        await bus.InvokeAsync(new DeactivateCustomFieldDefinitionCommand(id), cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Reorder custom fields for an entity type.
    /// </summary>
    [HttpPost("{entityType}/reorder")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Reorder(
        string entityType,
        ReorderFieldsRequest request,
        CancellationToken cancellationToken)
    {
        await bus.InvokeAsync(
            new ReorderCustomFieldsCommand(entityType, request.FieldIds),
            cancellationToken);
        return NoContent();
    }
}

#region Request DTOs

public sealed record CreateCustomFieldRequest
{
    public required string EntityType { get; init; }
    public required string FieldKey { get; init; }
    public required string DisplayName { get; init; }
    public required CustomFieldType FieldType { get; init; }
    public string? Description { get; init; }
    public bool IsRequired { get; init; }
    public FieldValidationRules? ValidationRules { get; init; }
    public IReadOnlyList<CustomFieldOption>? Options { get; init; }
}

public sealed record UpdateCustomFieldRequest
{
    public string? DisplayName { get; init; }
    public string? Description { get; init; }
    public bool ClearDescription { get; init; }
    public bool? IsRequired { get; init; }
    public int? DisplayOrder { get; init; }
    public FieldValidationRules? ValidationRules { get; init; }
    public IReadOnlyList<CustomFieldOption>? Options { get; init; }
}

public sealed record ReorderFieldsRequest
{
    public required IReadOnlyList<Guid> FieldIds { get; init; }
}

#endregion
