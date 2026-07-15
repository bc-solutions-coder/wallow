namespace Wallow.Shared.Kernel.CustomFields;

/// <summary>
/// Supported custom field data types.
/// </summary>
public enum CustomFieldType
{
    /// <summary>Single-line text input</summary>
    Text = 0,

    /// <summary>Multi-line text input</summary>
    TextArea = 1,

    /// <summary>Integer number</summary>
    Number = 2,

    /// <summary>Decimal number with precision</summary>
    Decimal = 3,

    /// <summary>Date only (no time)</summary>
    Date = 4,

    /// <summary>Date and time</summary>
    DateTime = 5,

    /// <summary>True/false toggle</summary>
    Boolean = 6,

    /// <summary>Single selection from predefined options</summary>
    Dropdown = 7,

    /// <summary>Multiple selections from predefined options</summary>
    MultiSelect = 8,

    /// <summary>Email address with format validation</summary>
    Email = 9,

    /// <summary>URL with format validation</summary>
    Url = 10,

    /// <summary>Phone number</summary>
    Phone = 11
}
