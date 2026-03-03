using Asp.Versioning;
using Foundry.Configuration.Application.Commands;
using Foundry.Configuration.Application.Contracts.DTOs;
using Foundry.Configuration.Application.Queries;
using Foundry.Shared.Kernel.CustomFields;
using Foundry.Shared.Kernel.Identity.Authorization;
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
public class CustomFieldsController : ControllerBase
{
    private readonly IMessageBus _bus;

    public CustomFieldsController(IMessageBus bus)
    {
        _bus = bus;
    }

    /// <summary>
    /// Get all entity types that support custom fields.
    /// </summary>
    [HttpGet("entity-types")]
    [ProducesResponseType(typeof(IReadOnlyList<EntityTypeDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<EntityTypeDto>>> GetEntityTypes(
        CancellationToken cancellationToken)
    {
        IReadOnlyList<EntityTypeDto> result = await _bus.InvokeAsync<IReadOnlyList<EntityTypeDto>>(
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
        IReadOnlyList<CustomFieldDefinitionDto> result = await _bus.InvokeAsync<IReadOnlyList<CustomFieldDefinitionDto>>(
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
        CustomFieldDefinitionDto? result = await _bus.InvokeAsync<CustomFieldDefinitionDto?>(
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
    public async Task<ActionResult<CustomFieldDefinitionDto>> Create(
        CreateCustomFieldRequest request,
        CancellationToken cancellationToken)
    {
        CreateCustomFieldDefinition command = new CreateCustomFieldDefinition(
            request.EntityType,
            request.FieldKey,
            request.DisplayName,
            request.FieldType,
            request.Description,
            request.IsRequired,
            request.ValidationRules,
            request.Options);

        CustomFieldDefinitionDto result = await _bus.InvokeAsync<CustomFieldDefinitionDto>(command, cancellationToken);

        return CreatedAtAction(
            nameof(GetById),
            new { id = result.Id },
            result);
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
        UpdateCustomFieldDefinition command = new UpdateCustomFieldDefinition(
            id,
            request.DisplayName,
            request.Description,
            request.ClearDescription,
            request.IsRequired,
            request.DisplayOrder,
            request.ValidationRules,
            request.Options);

        CustomFieldDefinitionDto result = await _bus.InvokeAsync<CustomFieldDefinitionDto>(command, cancellationToken);
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
        await _bus.InvokeAsync(new DeactivateCustomFieldDefinition(id), cancellationToken);
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
        await _bus.InvokeAsync(
            new ReorderCustomFields(entityType, request.FieldIds),
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
