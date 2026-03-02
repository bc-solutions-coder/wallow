using Foundry.Billing.Domain.Metering.Enums;
using Foundry.Billing.Domain.Metering.Identity;
using Foundry.Shared.Kernel.Domain;

namespace Foundry.Billing.Domain.Metering.Entities;

/// <summary>
/// Defines a meter that can be tracked and potentially limited.
/// Meters are system-defined and represent billable or limitable usage.
/// </summary>
public sealed class MeterDefinition : AuditableEntity<MeterDefinitionId>
{
    /// <summary>
    /// Unique code for this meter (e.g., "api.calls", "storage.bytes").
    /// </summary>
    public string Code { get; private set; } = string.Empty;

    /// <summary>
    /// Human-readable name for display (e.g., "API Calls").
    /// </summary>
    public string DisplayName { get; private set; } = string.Empty;

    /// <summary>
    /// Unit of measurement (e.g., "requests", "bytes").
    /// </summary>
    public string Unit { get; private set; } = string.Empty;

    /// <summary>
    /// How values are aggregated over a period.
    /// </summary>
    public MeterAggregation Aggregation { get; private set; }

    /// <summary>
    /// Whether this meter feeds into billing calculations.
    /// </summary>
    public bool IsBillable { get; private set; }

    /// <summary>
    /// Optional pattern for Valkey key generation.
    /// </summary>
    public string? ValkeyKeyPattern { get; private set; }

    private MeterDefinition() { } // EF Core

    private MeterDefinition(
        string code,
        string displayName,
        string unit,
        MeterAggregation aggregation,
        bool isBillable,
        string? valkeyKeyPattern,
        Guid createdByUserId)
    {
        Id = MeterDefinitionId.New();
        Code = code;
        DisplayName = displayName;
        Unit = unit;
        Aggregation = aggregation;
        IsBillable = isBillable;
        ValkeyKeyPattern = valkeyKeyPattern;
        SetCreated(DateTimeOffset.UtcNow, createdByUserId);
    }

    public static MeterDefinition Create(
        string code,
        string displayName,
        string unit,
        MeterAggregation aggregation,
        bool isBillable,
        string? valkeyKeyPattern = null,
        Guid? createdByUserId = null)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new BusinessRuleException(
                "Metering.CodeRequired",
                "Meter code cannot be empty");
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new BusinessRuleException(
                "Metering.DisplayNameRequired",
                "Meter display name cannot be empty");
        }

        if (string.IsNullOrWhiteSpace(unit))
        {
            throw new BusinessRuleException(
                "Metering.UnitRequired",
                "Meter unit cannot be empty");
        }

        return new MeterDefinition(
            code,
            displayName,
            unit,
            aggregation,
            isBillable,
            valkeyKeyPattern,
            createdByUserId ?? Guid.Empty);
    }

    public void Update(
        string displayName,
        string unit,
        MeterAggregation aggregation,
        bool isBillable,
        string? valkeyKeyPattern,
        Guid updatedByUserId)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new BusinessRuleException(
                "Metering.DisplayNameRequired",
                "Meter display name cannot be empty");
        }

        if (string.IsNullOrWhiteSpace(unit))
        {
            throw new BusinessRuleException(
                "Metering.UnitRequired",
                "Meter unit cannot be empty");
        }

        DisplayName = displayName;
        Unit = unit;
        Aggregation = aggregation;
        IsBillable = isBillable;
        ValkeyKeyPattern = valkeyKeyPattern;
        SetUpdated(DateTimeOffset.UtcNow, updatedByUserId);
    }
}
