namespace Wallow.Shared.Kernel.CustomFields;

/// <summary>
/// Marker interface for entities that support tenant-configurable custom fields.
/// Custom fields are stored as JSONB and validated against tenant's field definitions.
/// </summary>
public interface IHasCustomFields
{
    /// <summary>
    /// Flexible key-value storage for tenant-specific custom fields.
    /// Keys are field keys (snake_case), values are the field values.
    /// </summary>
    Dictionary<string, object>? CustomFields { get; }

    /// <summary>
    /// Sets the custom fields dictionary.
    /// </summary>
    void SetCustomFields(Dictionary<string, object>? customFields);
}
