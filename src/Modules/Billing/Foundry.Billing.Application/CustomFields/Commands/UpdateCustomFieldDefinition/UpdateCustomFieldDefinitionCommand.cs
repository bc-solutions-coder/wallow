using Foundry.Billing.Application.CustomFields.DTOs;
using Foundry.Billing.Application.CustomFields.Interfaces;
using Foundry.Billing.Domain.CustomFields.Entities;
using Foundry.Billing.Domain.CustomFields.Exceptions;
using Foundry.Billing.Domain.CustomFields.Identity;
using Foundry.Shared.Kernel.CustomFields;
using Foundry.Shared.Kernel.Services;

namespace Foundry.Billing.Application.CustomFields.Commands.UpdateCustomFieldDefinition;

public sealed record UpdateCustomFieldDefinitionCommand(
    Guid Id,
    string? DisplayName = null,
    string? Description = null,
    bool ClearDescription = false,
    bool? IsRequired = null,
    int? DisplayOrder = null,
    FieldValidationRules? ValidationRules = null,
    IReadOnlyList<CustomFieldOption>? Options = null);

public sealed class UpdateCustomFieldDefinitionHandler(
    ICustomFieldDefinitionRepository repository,
    ICurrentUserService currentUserService,
    TimeProvider timeProvider)
{

    public async Task<CustomFieldDefinitionDto> Handle(
        UpdateCustomFieldDefinitionCommand command,
        CancellationToken cancellationToken)
    {
        CustomFieldDefinition? definition = await repository.GetByIdAsync(
            CustomFieldDefinitionId.Create(command.Id),
            cancellationToken);

        if (definition == null)
        {
            throw new CustomFieldException($"Custom field definition with ID '{command.Id}' not found");
        }

        Guid userId = currentUserService.GetCurrentUserId() ?? Guid.Empty;

        if (command.DisplayName != null)
        {
            definition.UpdateDisplayName(command.DisplayName, userId, timeProvider);
        }

        if (command.Description != null || command.ClearDescription)
        {
            definition.UpdateDescription(command.Description, userId, timeProvider);
        }

        if (command.IsRequired.HasValue)
        {
            definition.SetRequired(command.IsRequired.Value, userId, timeProvider);
        }

        if (command.DisplayOrder.HasValue)
        {
            definition.SetDisplayOrder(command.DisplayOrder.Value, userId, timeProvider);
        }

        if (command.ValidationRules != null)
        {
            definition.SetValidationRules(command.ValidationRules, userId, timeProvider);
        }

        if (command.Options != null)
        {
            definition.SetOptions(command.Options, userId, timeProvider);
        }

        await repository.UpdateAsync(definition, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        return definition.ToDto();
    }
}
