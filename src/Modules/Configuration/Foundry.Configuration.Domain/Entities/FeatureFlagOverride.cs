using Foundry.Configuration.Domain.Identity;
using Foundry.Shared.Kernel.Domain;

namespace Foundry.Configuration.Domain.Entities;

/// <summary>
/// Override a flag's default value for a specific tenant or user.
/// </summary>
public sealed class FeatureFlagOverride : Entity<FeatureFlagOverrideId>
{
    public FeatureFlagId FlagId { get; private set; }
    public FeatureFlag Flag { get; private set; } = null!;

    /// <summary>Tenant-level override. Null means user-level only.</summary>
    public Guid? TenantId { get; private set; }

    /// <summary>User-level override. Null means tenant-level only.</summary>
    public Guid? UserId { get; private set; }

    /// <summary>Override enabled state (for Boolean/Percentage flags).</summary>
    public bool? IsEnabled { get; private set; }

    /// <summary>Override variant (for Variant flags).</summary>
    public string? Variant { get; private set; }

    /// <summary>Optional expiration for temporary overrides.</summary>
    public DateTime? ExpiresAt { get; private set; }

    public DateTime CreatedAt { get; private set; }

    private FeatureFlagOverride() { } // EF Core

    public static FeatureFlagOverride CreateForTenant(
        FeatureFlagId flagId,
        Guid tenantId,
        bool? isEnabled,
        TimeProvider timeProvider,
        string? variant = null,
        DateTime? expiresAt = null)
    {
        return new FeatureFlagOverride
        {
            Id = FeatureFlagOverrideId.New(),
            FlagId = flagId,
            TenantId = tenantId,
            UserId = null,
            IsEnabled = isEnabled,
            Variant = variant,
            ExpiresAt = expiresAt,
            CreatedAt = timeProvider.GetUtcNow().UtcDateTime
        };
    }

    public static FeatureFlagOverride CreateForUser(
        FeatureFlagId flagId,
        Guid userId,
        bool? isEnabled,
        TimeProvider timeProvider,
        string? variant = null,
        DateTime? expiresAt = null)
    {
        return new FeatureFlagOverride
        {
            Id = FeatureFlagOverrideId.New(),
            FlagId = flagId,
            TenantId = null,
            UserId = userId,
            IsEnabled = isEnabled,
            Variant = variant,
            ExpiresAt = expiresAt,
            CreatedAt = timeProvider.GetUtcNow().UtcDateTime
        };
    }

    public static FeatureFlagOverride CreateForTenantUser(
        FeatureFlagId flagId,
        Guid tenantId,
        Guid userId,
        bool? isEnabled,
        TimeProvider timeProvider,
        string? variant = null,
        DateTime? expiresAt = null)
    {
        return new FeatureFlagOverride
        {
            Id = FeatureFlagOverrideId.New(),
            FlagId = flagId,
            TenantId = tenantId,
            UserId = userId,
            IsEnabled = isEnabled,
            Variant = variant,
            ExpiresAt = expiresAt,
            CreatedAt = timeProvider.GetUtcNow().UtcDateTime
        };
    }

    public bool IsExpired(TimeProvider timeProvider) => ExpiresAt < timeProvider.GetUtcNow().UtcDateTime;
}
