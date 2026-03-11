using Asp.Versioning;
using Foundry.Billing.Application.CustomFields.Commands.CreateCustomFieldDefinition;
using Foundry.Billing.Application.CustomFields.Commands.DeactivateCustomFieldDefinition;
using Foundry.Billing.Application.CustomFields.Commands.ReorderCustomFields;
using Foundry.Billing.Application.CustomFields.Commands.UpdateCustomFieldDefinition;
using Foundry.Billing.Application.CustomFields.DTOs;
using Foundry.Billing.Application.CustomFields.Queries.GetCustomFieldDefinitionById;
using Foundry.Billing.Application.CustomFields.Queries.GetCustomFieldDefinitions;
using Foundry.Shared.Api.Extensions;
using Foundry.Shared.Kernel.CustomFields;
using Foundry.Shared.Kernel.Identity.Authorization;
using Foundry.Shared.Kernel.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace Foundry.Billing.Api.Controllers;

[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/billing/custom-field-definitions")]
[Authorize]
[HasPermission(PermissionType.ConfigurationManage)]
[Tags("Custom Field Definitions")]
[Produces("application/json")]
[Consumes("application/json")]
public class CustomFieldDefinitionsController(IMessageBus bus) : ControllerBase
{

    [HttpGet("{entityType}")]
    [ProducesResponseType(typeof(IReadOnlyList<CustomFieldDefinitionDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CustomFieldDefinitionDto>>> GetByEntityType(
        string entityType,
        [FromQuery] bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<CustomFieldDefinitionDto> result = await bus.InvokeAsync<IReadOnlyList<CustomFieldDefinitionDto>>(
            new GetCustomFieldDefinitionsQuery(entityType, includeInactive),
            cancellationToken);
        return Ok(result);
    }

    [HttpGet("by-id/{id:guid}")]
    [ProducesResponseType(typeof(CustomFieldDefinitionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CustomFieldDefinitionDto>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        CustomFieldDefinitionDto? result = await bus.InvokeAsync<CustomFieldDefinitionDto?>(
            new GetCustomFieldDefinitionByIdQuery(id),
            cancellationToken);

        if (result == null)
        {
            return NotFound();
        }

        return Ok(result);
    }

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
            "CustomFieldDefinitions",
            dto => new { id = dto.Id });
    }

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
