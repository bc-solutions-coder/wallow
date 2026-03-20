using Wallow.Billing.Domain.CustomFields.Entities;

namespace Wallow.Billing.Application.CustomFields.DTOs;

public static class CustomFieldDefinitionMapper
{
    public static CustomFieldDefinitionDto ToDto(this CustomFieldDefinition entity)
    {
        return new CustomFieldDefinitionDto
        {
            Id = entity.Id.Value,
            EntityType = entity.EntityType,
            FieldKey = entity.FieldKey,
            DisplayName = entity.DisplayName,
            Description = entity.Description,
            FieldType = entity.FieldType,
            DisplayOrder = entity.DisplayOrder,
            IsRequired = entity.IsRequired,
            IsActive = entity.IsActive,
            ValidationRules = entity.GetValidationRules(),
            Options = entity.GetOptions(),
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };
    }

    public static IReadOnlyList<CustomFieldDefinitionDto> ToDtoList(
        this IEnumerable<CustomFieldDefinition> entities)
    {
        return entities.Select(ToDto).ToList();
    }
}
