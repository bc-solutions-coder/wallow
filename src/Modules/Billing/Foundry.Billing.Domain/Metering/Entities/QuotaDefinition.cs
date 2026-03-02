using Foundry.Billing.Domain.Metering.Enums;
using Foundry.Billing.Domain.Metering.Identity;
using Foundry.Shared.Kernel.Domain;
using Foundry.Shared.Kernel.Identity;
using Foundry.Shared.Kernel.MultiTenancy;

namespace Foundry.Billing.Domain.Metering.Entities;

/// <summary>
/// Defines usage limits per plan or tenant override.
/// Tenant-specific overrides take precedence over plan defaults.
/// </summary>
public sealed class QuotaDefinition : AuditableEntity<QuotaDefinitionId>, ITenantScoped
{
    /// <summary>
    /// The meter code this quota applies to.
    /// </summary>
    public string MeterCode { get; private set; } = string.Empty;

    /// <summary>
    /// Plan code (e.g., "free", "pro", "enterprise"). Null for default quota.
    /// </summary>
    public string? PlanCode { get; private set; }

    /// <summary>
    /// Tenant for tenant-specific overrides. Takes precedence over plan.
    /// </summary>
    public TenantId TenantId { get; set; }

    /// <summary>
    /// The usage limit for this period.
    /// </summary>
    public decimal Limit { get; private set; }

    /// <summary>
    /// The time period over which the quota is evaluated.
    /// </summary>
    public QuotaPeriod Period { get; private set; }

    /// <summary>
    /// Action to take when quota is exceeded.
    /// </summary>
    public QuotaAction OnExceeded { get; private set; }

    private QuotaDefinition() { } // EF Core

    private QuotaDefinition(
        string meterCode,
        string? planCode,
        TenantId? tenantId,
        decimal limit,
        QuotaPeriod period,
        QuotaAction onExceeded,
        Guid createdByUserId)
    {
        Id = QuotaDefinitionId.New();
        MeterCode = meterCode;
        PlanCode = planCode;
        TenantId = tenantId ?? TenantId.Create(Guid.Empty);
        Limit = limit;
        Period = period;
        OnExceeded = onExceeded;
        SetCreated(DateTimeOffset.UtcNow, createdByUserId);
    }

    public static QuotaDefinition CreatePlanQuota(
        string meterCode,
        string planCode,
        decimal limit,
        QuotaPeriod period,
        QuotaAction onExceeded,
        Guid? createdByUserId = null)
    {
        if (string.IsNullOrWhiteSpace(meterCode))
        {
            throw new BusinessRuleException(
                "Metering.MeterCodeRequired",
                "Meter code cannot be empty");
        }

        if (string.IsNullOrWhiteSpace(planCode))
        {
            throw new BusinessRuleException(
                "Metering.PlanCodeRequired",
                "Plan code cannot be empty for plan quota");
        }

        if (limit < 0)
        {
            throw new BusinessRuleException(
                "Metering.InvalidLimit",
                "Quota limit must be non-negative");
        }

        return new QuotaDefinition(
            meterCode,
            planCode,
            null,
            limit,
            period,
            onExceeded,
            createdByUserId ?? Guid.Empty);
    }

    public static QuotaDefinition CreateTenantOverride(
        string meterCode,
        TenantId tenantId,
        decimal limit,
        QuotaPeriod period,
        QuotaAction onExceeded,
        Guid? createdByUserId = null)
    {
        if (string.IsNullOrWhiteSpace(meterCode))
        {
            throw new BusinessRuleException(
                "Metering.MeterCodeRequired",
                "Meter code cannot be empty");
        }

        if (tenantId.Value == Guid.Empty)
        {
            throw new BusinessRuleException(
                "Metering.TenantIdRequired",
                "Tenant ID is required for tenant override");
        }

        if (limit < 0)
        {
            throw new BusinessRuleException(
                "Metering.InvalidLimit",
                "Quota limit must be non-negative");
        }

        return new QuotaDefinition(
            meterCode,
            null,
            tenantId,
            limit,
            period,
            onExceeded,
            createdByUserId ?? Guid.Empty);
    }

    public void UpdateLimit(decimal limit, QuotaAction onExceeded, Guid updatedByUserId)
    {
        if (limit < 0)
        {
            throw new BusinessRuleException(
                "Metering.InvalidLimit",
                "Quota limit must be non-negative");
        }

        Limit = limit;
        OnExceeded = onExceeded;
        SetUpdated(DateTimeOffset.UtcNow, updatedByUserId);
    }
}
