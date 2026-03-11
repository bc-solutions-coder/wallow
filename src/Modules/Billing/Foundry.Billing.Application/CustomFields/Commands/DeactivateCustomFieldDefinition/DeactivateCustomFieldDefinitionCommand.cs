using Foundry.Billing.Application.CustomFields.Interfaces;
using Foundry.Billing.Domain.CustomFields.Entities;
using Foundry.Billing.Domain.CustomFields.Exceptions;
using Foundry.Billing.Domain.CustomFields.Identity;

namespace Foundry.Billing.Application.CustomFields.Commands.DeactivateCustomFieldDefinition;

public sealed record DeactivateCustomFieldDefinitionCommand(Guid Id);

public sealed class DeactivateCustomFieldDefinitionHandler(
    ICustomFieldDefinitionRepository repository,
    TimeProvider timeProvider)
{

    public async Task Handle(
        DeactivateCustomFieldDefinitionCommand command,
        CancellationToken cancellationToken)
    {
        CustomFieldDefinition? definition = await repository.GetByIdAsync(
            CustomFieldDefinitionId.Create(command.Id),
            cancellationToken);

        if (definition == null)
        {
            throw new CustomFieldException($"Custom field definition with ID '{command.Id}' not found");
        }

        definition.Deactivate(Guid.Empty, timeProvider);

        await repository.UpdateAsync(definition, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
    }
}
