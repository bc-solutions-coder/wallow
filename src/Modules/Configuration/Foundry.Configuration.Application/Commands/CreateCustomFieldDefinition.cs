using Foundry.Configuration.Application.Contracts;
using Foundry.Configuration.Application.Contracts.DTOs;
using Foundry.Configuration.Domain.Entities;
using Foundry.Configuration.Domain.Exceptions;
using Foundry.Shared.Kernel.CustomFields;
using Foundry.Shared.Kernel.MultiTenancy;

namespace Foundry.Configuration.Application.Commands;

public sealed record CreateCustomFieldDefinition(
    string EntityType,
    string FieldKey,
    string DisplayName,
    CustomFieldType FieldType,
    string? Description = null,
    bool IsRequired = false,
    FieldValidationRules? ValidationRules = null,
    IReadOnlyList<CustomFieldOption>? Options = null);

public sealed class CreateCustomFieldDefinitionHandler
{
    private readonly ICustomFieldDefinitionRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly TimeProvider _timeProvider;

    public CreateCustomFieldDefinitionHandler(
        ICustomFieldDefinitionRepository repository,
        ITenantContext tenantContext,
        TimeProvider timeProvider)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _timeProvider = timeProvider;
    }

    public async Task<CustomFieldDefinitionDto> Handle(
        CreateCustomFieldDefinition command,
        CancellationToken cancellationToken)
    {
        if (await _repository.FieldKeyExistsAsync(command.EntityType, command.FieldKey, cancellationToken))
        {
            throw new CustomFieldException(
                $"A field with key '{command.FieldKey}' already exists for entity type '{command.EntityType}'");
        }

        CustomFieldDefinition definition = CustomFieldDefinition.Create(
            _tenantContext.TenantId,
            command.EntityType,
            command.FieldKey,
            command.DisplayName,
            command.FieldType,
            Guid.Empty,
            _timeProvider);

        if (!string.IsNullOrWhiteSpace(command.Description))
        {
            definition.UpdateDescription(command.Description, Guid.Empty, _timeProvider);
        }

        if (command.IsRequired)
        {
            definition.SetRequired(true, Guid.Empty, _timeProvider);
        }

        if (command.ValidationRules != null)
        {
            definition.SetValidationRules(command.ValidationRules, Guid.Empty, _timeProvider);
        }

        if (command.Options != null && command.Options.Count > 0)
        {
            definition.SetOptions(command.Options, Guid.Empty, _timeProvider);
        }

        await _repository.AddAsync(definition, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        return definition.ToDto();
    }
}
