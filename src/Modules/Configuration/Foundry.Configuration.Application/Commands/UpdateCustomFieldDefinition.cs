using Foundry.Configuration.Application.Contracts;
using Foundry.Configuration.Application.Contracts.DTOs;
using Foundry.Configuration.Domain.Entities;
using Foundry.Configuration.Domain.Exceptions;
using Foundry.Configuration.Domain.Identity;
using Foundry.Shared.Kernel.CustomFields;
using Foundry.Shared.Kernel.Services;

namespace Foundry.Configuration.Application.Commands;

public sealed record UpdateCustomFieldDefinition(
    Guid Id,
    string? DisplayName = null,
    string? Description = null,
    bool ClearDescription = false,
    bool? IsRequired = null,
    int? DisplayOrder = null,
    FieldValidationRules? ValidationRules = null,
    IReadOnlyList<CustomFieldOption>? Options = null);

public sealed class UpdateCustomFieldDefinitionHandler
{
    private readonly ICustomFieldDefinitionRepository _repository;
    private readonly ICurrentUserService _currentUserService;
    private readonly TimeProvider _timeProvider;

    public UpdateCustomFieldDefinitionHandler(
        ICustomFieldDefinitionRepository repository,
        ICurrentUserService currentUserService,
        TimeProvider timeProvider)
    {
        _repository = repository;
        _currentUserService = currentUserService;
        _timeProvider = timeProvider;
    }

    public async Task<CustomFieldDefinitionDto> Handle(
        UpdateCustomFieldDefinition command,
        CancellationToken cancellationToken)
    {
        CustomFieldDefinition? definition = await _repository.GetByIdAsync(
            CustomFieldDefinitionId.Create(command.Id),
            cancellationToken);

        if (definition == null)
        {
            throw new CustomFieldException($"Custom field definition with ID '{command.Id}' not found");
        }

        Guid userId = _currentUserService.GetCurrentUserId() ?? Guid.Empty;

        if (command.DisplayName != null)
        {
            definition.UpdateDisplayName(command.DisplayName, userId, _timeProvider);
        }

        if (command.Description != null || command.ClearDescription)
        {
            definition.UpdateDescription(command.Description, userId, _timeProvider);
        }

        if (command.IsRequired.HasValue)
        {
            definition.SetRequired(command.IsRequired.Value, userId, _timeProvider);
        }

        if (command.DisplayOrder.HasValue)
        {
            definition.SetDisplayOrder(command.DisplayOrder.Value, userId, _timeProvider);
        }

        if (command.ValidationRules != null)
        {
            definition.SetValidationRules(command.ValidationRules, userId, _timeProvider);
        }

        if (command.Options != null)
        {
            definition.SetOptions(command.Options, userId, _timeProvider);
        }

        await _repository.UpdateAsync(definition, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        return definition.ToDto();
    }
}
