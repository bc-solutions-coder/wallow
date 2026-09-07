namespace Wallow.Shared.Kernel.CustomFields;

/// <summary>
/// Tenant-configurable custom field values. Implementations must arrange persistence
/// and validation against the tenant's field definitions.
/// </summary>
public interface IHasCustomFields
{
    /// <summary>
    /// Flexible key-value storage for tenant-specific custom fields.
    /// Keys are field keys (snake_case), values are the field values.
    /// </summary>
    Dictionary<string, object>? CustomFields { get; }

    void SetCustomFields(Dictionary<string, object>? customFields);
}
