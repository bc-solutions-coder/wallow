using Foundry.Configuration.Application.Contracts;
using Foundry.Configuration.Domain.Entities;
using Foundry.Configuration.Domain.Exceptions;
using Foundry.Configuration.Domain.Identity;

namespace Foundry.Configuration.Application.Commands;

public sealed record DeactivateCustomFieldDefinition(Guid Id);

public sealed class DeactivateCustomFieldDefinitionHandler
{
    private readonly ICustomFieldDefinitionRepository _repository;
    private readonly TimeProvider _timeProvider;

    public DeactivateCustomFieldDefinitionHandler(ICustomFieldDefinitionRepository repository, TimeProvider timeProvider)
    {
        _repository = repository;
        _timeProvider = timeProvider;
    }

    public async Task Handle(
        DeactivateCustomFieldDefinition command,
        CancellationToken cancellationToken)
    {
        CustomFieldDefinition? definition = await _repository.GetByIdAsync(
            CustomFieldDefinitionId.Create(command.Id),
            cancellationToken);

        if (definition == null)
        {
            throw new CustomFieldException($"Custom field definition with ID '{command.Id}' not found");
        }

        definition.Deactivate(Guid.Empty, _timeProvider);

        await _repository.UpdateAsync(definition, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);
    }
}
