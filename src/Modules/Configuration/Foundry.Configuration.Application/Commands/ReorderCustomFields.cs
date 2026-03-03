using Foundry.Configuration.Application.Contracts;
using Foundry.Configuration.Domain.Entities;
using Foundry.Configuration.Domain.Exceptions;

namespace Foundry.Configuration.Application.Commands;

public sealed record ReorderCustomFields(string EntityType, IReadOnlyList<Guid> FieldIdsInOrder);

public sealed class ReorderCustomFieldsHandler
{
    private readonly ICustomFieldDefinitionRepository _repository;
    private readonly TimeProvider _timeProvider;

    public ReorderCustomFieldsHandler(ICustomFieldDefinitionRepository repository, TimeProvider timeProvider)
    {
        _repository = repository;
        _timeProvider = timeProvider;
    }

    public async Task Handle(
        ReorderCustomFields command,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<CustomFieldDefinition> definitions = await _repository.GetByEntityTypeAsync(
            command.EntityType,
            includeInactive: true,
            cancellationToken);

        Dictionary<Guid, CustomFieldDefinition> definitionsById = definitions.ToDictionary(d => d.Id.Value);
        Guid userId = Guid.Empty;

        for (int i = 0; i < command.FieldIdsInOrder.Count; i++)
        {
            Guid fieldId = command.FieldIdsInOrder[i];

            if (!definitionsById.TryGetValue(fieldId, out CustomFieldDefinition? definition))
            {
                throw new CustomFieldException($"Field with ID '{fieldId}' not found for entity type '{command.EntityType}'");
            }

            definition.SetDisplayOrder(i, userId, _timeProvider);
            await _repository.UpdateAsync(definition, cancellationToken);
        }

        await _repository.SaveChangesAsync(cancellationToken);
    }
}
