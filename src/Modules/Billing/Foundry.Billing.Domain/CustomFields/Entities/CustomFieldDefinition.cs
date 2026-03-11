using System.Text.Json;
using System.Text.RegularExpressions;
using Foundry.Billing.Domain.CustomFields.Events;
using Foundry.Billing.Domain.CustomFields.Exceptions;
using Foundry.Billing.Domain.CustomFields.Identity;
using Foundry.Shared.Kernel.CustomFields;
using Foundry.Shared.Kernel.Domain;
using Foundry.Shared.Kernel.Identity;
using Foundry.Shared.Kernel.MultiTenancy;

namespace Foundry.Billing.Domain.CustomFields.Entities;

/// <summary>
/// Defines a custom field that a tenant has configured for an entity type.
/// </summary>
public sealed partial class CustomFieldDefinition : AggregateRoot<CustomFieldDefinitionId>, ITenantScoped
{
    [GeneratedRegex(@"^[a-z][a-z0-9_]*$", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex FieldKeyPattern();

    public TenantId TenantId { get; init; }

    /// <summary>Entity type this field belongs to (e.g., "Invoice", "Payment")</summary>
    public string EntityType { get; private set; } = string.Empty;

    /// <summary>Unique key for this field within the entity type (snake_case)</summary>
    public string FieldKey { get; private set; } = string.Empty;

    /// <summary>Display name shown in UI</summary>
    public string DisplayName { get; private set; } = string.Empty;

    /// <summary>Optional description/help text</summary>
    public string? Description { get; private set; }

    /// <summary>Data type of this field</summary>
    public CustomFieldType FieldType { get; private set; }

    /// <summary>Order in which this field appears in forms</summary>
    public int DisplayOrder { get; private set; }

    /// <summary>Whether this field must have a value</summary>
    public bool IsRequired { get; private set; }

    /// <summary>Whether this field is currently active (soft delete)</summary>
    public bool IsActive { get; private set; } = true;

    /// <summary>Validation rules as JSON</summary>
    public string? ValidationRulesJson { get; private set; }

    /// <summary>Options for dropdown/multi-select as JSON</summary>
    public string? OptionsJson { get; private set; }

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private CustomFieldDefinition() { }

    public static CustomFieldDefinition Create(
        TenantId tenantId,
        string entityType,
        string fieldKey,
        string displayName,
        CustomFieldType fieldType,
        Guid createdBy,
        TimeProvider timeProvider)
    {
        ValidateEntityType(entityType);
        ValidateFieldKey(fieldKey);
        ValidateDisplayName(displayName);

        CustomFieldDefinition definition = new()
        {
            Id = CustomFieldDefinitionId.New(),
            TenantId = tenantId,
            EntityType = entityType,
            FieldKey = fieldKey,
            DisplayName = displayName,
            FieldType = fieldType,
            DisplayOrder = 0,
            IsRequired = false,
            IsActive = true
        };

        definition.SetCreated(timeProvider.GetUtcNow(), createdBy);
        definition.RaiseDomainEvent(new CustomFieldDefinitionCreatedEvent(
            definition.Id.Value, tenantId.Value, entityType, fieldKey, displayName, fieldType));

        return definition;
    }

    public void UpdateDisplayName(string displayName, Guid updatedBy, TimeProvider timeProvider)
    {
        ValidateDisplayName(displayName);
        DisplayName = displayName;
        SetUpdated(timeProvider.GetUtcNow(), updatedBy);
    }

    public void UpdateDescription(string? description, Guid updatedBy, TimeProvider timeProvider)
    {
        Description = description;
        SetUpdated(timeProvider.GetUtcNow(), updatedBy);
    }

    public void SetRequired(bool isRequired, Guid updatedBy, TimeProvider timeProvider)
    {
        IsRequired = isRequired;
        SetUpdated(timeProvider.GetUtcNow(), updatedBy);
    }

    public void SetDisplayOrder(int order, Guid updatedBy, TimeProvider timeProvider)
    {
        if (order < 0)
        {
            throw new CustomFieldException("Display order cannot be negative");
        }

        DisplayOrder = order;
        SetUpdated(timeProvider.GetUtcNow(), updatedBy);
    }

    public void SetValidationRules(FieldValidationRules? rules, Guid updatedBy, TimeProvider timeProvider)
    {
        if (rules != null)
        {
            ValidateRulesForFieldType(rules);
        }

        ValidationRulesJson = rules == null ? null : JsonSerializer.Serialize(rules, _jsonOptions);
        SetUpdated(timeProvider.GetUtcNow(), updatedBy);
    }

    public FieldValidationRules? GetValidationRules()
    {
        return ValidationRulesJson == null
            ? null
            : JsonSerializer.Deserialize<FieldValidationRules>(ValidationRulesJson, _jsonOptions);
    }

    public void SetOptions(IEnumerable<CustomFieldOption>? options, Guid updatedBy, TimeProvider timeProvider)
    {
        if (options != null && FieldType != CustomFieldType.Dropdown && FieldType != CustomFieldType.MultiSelect)
        {
            throw new CustomFieldException("Options are only allowed for Dropdown and MultiSelect field types");
        }

        List<CustomFieldOption>? optionsList = options?.ToList();
        if (optionsList != null && optionsList.Count > 0)
        {
            ValidateOptions(optionsList);
            OptionsJson = JsonSerializer.Serialize(optionsList, _jsonOptions);
        }
        else
        {
            OptionsJson = null;
        }
        SetUpdated(timeProvider.GetUtcNow(), updatedBy);
    }

    public IReadOnlyList<CustomFieldOption> GetOptions()
    {
        return OptionsJson == null
            ? []
            : JsonSerializer.Deserialize<List<CustomFieldOption>>(OptionsJson, _jsonOptions) ?? [];
    }

    public void Deactivate(Guid deactivatedBy, TimeProvider timeProvider)
    {
        if (!IsActive)
        {
            throw new CustomFieldException("Field is already deactivated");
        }

        IsActive = false;
        SetUpdated(timeProvider.GetUtcNow(), deactivatedBy);
        RaiseDomainEvent(new CustomFieldDefinitionDeactivatedEvent(Id.Value, TenantId.Value, EntityType, FieldKey));
    }

    public void Activate(Guid activatedBy, TimeProvider timeProvider)
    {
        if (IsActive)
        {
            throw new CustomFieldException("Field is already active");
        }

        IsActive = true;
        SetUpdated(timeProvider.GetUtcNow(), activatedBy);
    }

    private static void ValidateEntityType(string entityType)
    {
        if (string.IsNullOrWhiteSpace(entityType))
        {
            throw new CustomFieldException("Entity type is required");
        }

        if (!CustomFieldRegistry.IsSupported(entityType))
        {
            throw new CustomFieldException($"Entity type '{entityType}' does not support custom fields");
        }
    }

    private static void ValidateFieldKey(string fieldKey)
    {
        if (string.IsNullOrWhiteSpace(fieldKey))
        {
            throw new CustomFieldException("Field key is required");
        }

        if (fieldKey.Length > 50)
        {
            throw new CustomFieldException("Field key must be 50 characters or less");
        }

        if (!FieldKeyPattern().IsMatch(fieldKey))
        {
            throw new CustomFieldException("Field key must be lowercase alphanumeric with underscores, starting with a letter");
        }
    }

    private static void ValidateDisplayName(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new CustomFieldException("Display name is required");
        }

        if (displayName.Length > 100)
        {
            throw new CustomFieldException("Display name must be 100 characters or less");
        }
    }

    private void ValidateRulesForFieldType(FieldValidationRules rules)
    {
        // String length rules only for text types
        if ((rules.MinLength.HasValue || rules.MaxLength.HasValue) &&
            FieldType != CustomFieldType.Text &&
            FieldType != CustomFieldType.TextArea &&
            FieldType != CustomFieldType.Email &&
            FieldType != CustomFieldType.Url &&
            FieldType != CustomFieldType.Phone)
        {
            throw new CustomFieldException("MinLength/MaxLength rules only apply to text-based field types");
        }

        // Numeric range rules only for number types
        if ((rules.Min.HasValue || rules.Max.HasValue) &&
            FieldType != CustomFieldType.Number &&
            FieldType != CustomFieldType.Decimal)
        {
            throw new CustomFieldException("Min/Max rules only apply to numeric field types");
        }

        // Date range rules only for date types
        if ((rules.MinDate.HasValue || rules.MaxDate.HasValue) &&
            FieldType != CustomFieldType.Date &&
            FieldType != CustomFieldType.DateTime)
        {
            throw new CustomFieldException("MinDate/MaxDate rules only apply to date field types");
        }

        // Pattern only for text types
        if (rules.Pattern != null &&
            FieldType != CustomFieldType.Text &&
            FieldType != CustomFieldType.TextArea)
        {
            throw new CustomFieldException("Pattern rules only apply to text field types");
        }

        // Validate regex pattern is valid
        if (rules.Pattern != null)
        {
            try
            {
                _ = new Regex(rules.Pattern, RegexOptions.None, TimeSpan.FromSeconds(1));
            }
            catch (ArgumentException)
            {
                throw new CustomFieldException("Invalid regex pattern");
            }
        }
    }

    private static void ValidateOptions(List<CustomFieldOption> options)
    {
        if (options.Count == 0)
        {
            throw new CustomFieldException("At least one option is required for dropdown/multi-select fields");
        }

        List<string> duplicateValues = options.GroupBy(o => o.Value).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        if (duplicateValues.Count > 0)
        {
            throw new CustomFieldException($"Duplicate option values: {string.Join(", ", duplicateValues)}");
        }
    }
}
