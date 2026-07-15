namespace Wallow.Shared.Kernel.CustomFields;

/// <summary>
/// An option for dropdown or multi-select custom fields.
/// </summary>
public sealed record CustomFieldOption
{
    /// <summary>The value stored when this option is selected</summary>
    public required string Value { get; init; }

    /// <summary>Display label shown to users</summary>
    public required string Label { get; init; }

    /// <summary>Display order (lower = first)</summary>
    public int Order { get; init; }

    /// <summary>Whether this option is currently active</summary>
    public bool IsActive { get; init; } = true;
}
