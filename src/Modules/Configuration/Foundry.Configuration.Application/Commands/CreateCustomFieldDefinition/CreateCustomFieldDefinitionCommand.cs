using Foundry.Configuration.Application.Contracts;
using Foundry.Configuration.Application.Contracts.DTOs;
using Foundry.Configuration.Domain.Entities;
using Foundry.Shared.Kernel.CustomFields;
using Foundry.Shared.Kernel.MultiTenancy;
using Foundry.Shared.Kernel.Results;
using Foundry.Shared.Kernel.Services;

namespace Foundry.Configuration.Application.Commands.CreateCustomFieldDefinition;

public sealed record CreateCustomFieldDefinitionCommand(
    string EntityType,
    string FieldKey,
    string DisplayName,
    CustomFieldType FieldType,
    string? Description = null,
    bool IsRequired = false,
    FieldValidationRules? ValidationRules = null,
    IReadOnlyList<CustomFieldOption>? Options = null);

public sealed class CreateCustomFieldDefinitionHandler(
    ICustomFieldDefinitionRepository repository,
    ITenantContext tenantContext,
    ICurrentUserService currentUserService,
    TimeProvider timeProvider)
{

    public async Task<Result<CustomFieldDefinitionDto>> Handle(
        CreateCustomFieldDefinitionCommand command,
        CancellationToken cancellationToken)
    {
        if (await repository.FieldKeyExistsAsync(command.EntityType, command.FieldKey, cancellationToken))
        {
            return Result.Failure<CustomFieldDefinitionDto>(
                Error.Conflict(
                    $"A field with key '{command.FieldKey}' already exists for entity type '{command.EntityType}'"));
        }

        Guid userId = currentUserService.GetCurrentUserId() ?? Guid.Empty;

        CustomFieldDefinition definition = CustomFieldDefinition.Create(
            tenantContext.TenantId,
            command.EntityType,
            command.FieldKey,
            command.DisplayName,
            command.FieldType,
            userId,
            timeProvider);

        if (!string.IsNullOrWhiteSpace(command.Description))
        {
            definition.UpdateDescription(command.Description, userId, timeProvider);
        }

        if (command.IsRequired)
        {
            definition.SetRequired(true, userId, timeProvider);
        }

        if (command.ValidationRules != null)
        {
            definition.SetValidationRules(command.ValidationRules, userId, timeProvider);
        }

        if (command.Options != null && command.Options.Count > 0)
        {
            definition.SetOptions(command.Options, userId, timeProvider);
        }

        await repository.AddAsync(definition, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        return Result.Success(definition.ToDto());
    }
}
