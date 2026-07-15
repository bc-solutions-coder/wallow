namespace Wallow.Shared.Kernel.CustomFields;

/// <summary>
/// Validation rules for a custom field. Rules are type-specific.
/// </summary>
public sealed record FieldValidationRules
{
    /// <summary>Minimum length for text fields</summary>
    public int? MinLength { get; init; }

    /// <summary>Maximum length for text fields</summary>
    public int? MaxLength { get; init; }

    /// <summary>Minimum value for numeric fields</summary>
    public decimal? Min { get; init; }

    /// <summary>Maximum value for numeric fields</summary>
    public decimal? Max { get; init; }

    /// <summary>Regex pattern for text validation</summary>
    public string? Pattern { get; init; }

    /// <summary>User-friendly message when pattern validation fails</summary>
    public string? PatternMessage { get; init; }

    /// <summary>Minimum date for date fields</summary>
    public DateTime? MinDate { get; init; }

    /// <summary>Maximum date for date fields</summary>
    public DateTime? MaxDate { get; init; }
}
