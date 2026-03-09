using Foundry.Configuration.Application.Contracts;
using Foundry.Configuration.Domain.Entities;
using Foundry.Configuration.Domain.Exceptions;
using Foundry.Configuration.Domain.Identity;

namespace Foundry.Configuration.Application.Commands.DeactivateCustomFieldDefinition;

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
