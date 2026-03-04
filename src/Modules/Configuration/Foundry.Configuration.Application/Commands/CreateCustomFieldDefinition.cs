using Foundry.Configuration.Application.Contracts;
using Foundry.Configuration.Application.Contracts.DTOs;
using Foundry.Configuration.Domain.Entities;
using Foundry.Shared.Kernel.CustomFields;
using Foundry.Shared.Kernel.MultiTenancy;
using Foundry.Shared.Kernel.Results;
using Foundry.Shared.Kernel.Services;

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
    private readonly ICurrentUserService _currentUserService;
    private readonly TimeProvider _timeProvider;

    public CreateCustomFieldDefinitionHandler(
        ICustomFieldDefinitionRepository repository,
        ITenantContext tenantContext,
        ICurrentUserService currentUserService,
        TimeProvider timeProvider)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _currentUserService = currentUserService;
        _timeProvider = timeProvider;
    }

    public async Task<Result<CustomFieldDefinitionDto>> Handle(
        CreateCustomFieldDefinition command,
        CancellationToken cancellationToken)
    {
        if (await _repository.FieldKeyExistsAsync(command.EntityType, command.FieldKey, cancellationToken))
        {
            return Result.Failure<CustomFieldDefinitionDto>(
                Error.Conflict(
                    $"A field with key '{command.FieldKey}' already exists for entity type '{command.EntityType}'"));
        }

        Guid userId = _currentUserService.GetCurrentUserId() ?? Guid.Empty;

        CustomFieldDefinition definition = CustomFieldDefinition.Create(
            _tenantContext.TenantId,
            command.EntityType,
            command.FieldKey,
            command.DisplayName,
            command.FieldType,
            userId,
            _timeProvider);

        if (!string.IsNullOrWhiteSpace(command.Description))
        {
            definition.UpdateDescription(command.Description, userId, _timeProvider);
        }

        if (command.IsRequired)
        {
            definition.SetRequired(true, userId, _timeProvider);
        }

        if (command.ValidationRules != null)
        {
            definition.SetValidationRules(command.ValidationRules, userId, _timeProvider);
        }

        if (command.Options != null && command.Options.Count > 0)
        {
            definition.SetOptions(command.Options, userId, _timeProvider);
        }

        await _repository.AddAsync(definition, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        return Result.Success(definition.ToDto());
    }
}
