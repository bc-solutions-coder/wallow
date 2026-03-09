using Foundry.Configuration.Domain.Enums;
using Foundry.Configuration.Domain.Events;
using Foundry.Configuration.Domain.Exceptions;
using Foundry.Configuration.Domain.Identity;
using Foundry.Configuration.Domain.ValueObjects;
using Foundry.Shared.Kernel.Domain;

namespace Foundry.Configuration.Domain.Entities;

/// <summary>
/// A feature flag definition. Global to the platform (not tenant-scoped).
/// Tenants/users get overrides, not their own flags.
/// </summary>
public sealed class FeatureFlag : AggregateRoot<FeatureFlagId>
{
    public string Key { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }

    public FlagType FlagType { get; private set; }
    public bool DefaultEnabled { get; private set; }

    /// <summary>For Percentage type: 0-100.</summary>
    public int? RolloutPercentage { get; private set; }

    private readonly List<VariantWeight> _variants = new();

    /// <summary>For Variant type: weighted variant definitions.</summary>
    public IReadOnlyCollection<VariantWeight> Variants => _variants.AsReadOnly();

    /// <summary>For Variant type: which variant is default.</summary>
    public string? DefaultVariant { get; private set; }

    // ReSharper disable once CollectionNeverUpdated.Local — EF Core populates via navigation property mapping
    private readonly List<FeatureFlagOverride> _overrides = new();
    public IReadOnlyCollection<FeatureFlagOverride> Overrides => _overrides.AsReadOnly();

    private FeatureFlag() { } // EF Core

    public static FeatureFlag CreateBoolean(string key, string name, bool defaultEnabled, TimeProvider timeProvider, string? description = null)
    {
        FeatureFlag flag = new FeatureFlag
        {
            Id = FeatureFlagId.New(),
            Key = key,
            Name = name,
            Description = description,
            FlagType = FlagType.Boolean,
            DefaultEnabled = defaultEnabled,
            CreatedAt = timeProvider.GetUtcNow().UtcDateTime
        };

        flag.RaiseDomainEvent(new FeatureFlagCreatedEvent(flag.Id.Value, flag.Key, flag.FlagType));
        return flag;
    }

    public static FeatureFlag CreatePercentage(string key, string name, int percentage, TimeProvider timeProvider, string? description = null)
    {
        if (percentage < 0 || percentage > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(percentage), "Must be 0-100");
        }

        FeatureFlag flag = new FeatureFlag
        {
            Id = FeatureFlagId.New(),
            Key = key,
            Name = name,
            Description = description,
            FlagType = FlagType.Percentage,
            DefaultEnabled = percentage > 0,
            RolloutPercentage = percentage,
            CreatedAt = timeProvider.GetUtcNow().UtcDateTime
        };

        flag.RaiseDomainEvent(new FeatureFlagCreatedEvent(flag.Id.Value, flag.Key, flag.FlagType));
        return flag;
    }

    public static FeatureFlag CreateVariant(string key, string name, IReadOnlyList<VariantWeight> variants, string defaultVariant, TimeProvider timeProvider, string? description = null)
    {
        if (variants.Count == 0)
        {
            throw new ArgumentException("At least one variant required.", nameof(variants));
        }

        if (!variants.Any(v => v.Name == defaultVariant))
        {
            throw new ArgumentException("Default variant must be in variants list.", nameof(defaultVariant));
        }

        FeatureFlag flag = new FeatureFlag
        {
            Id = FeatureFlagId.New(),
            Key = key,
            Name = name,
            Description = description,
            FlagType = FlagType.Variant,
            DefaultEnabled = true,
            DefaultVariant = defaultVariant,
            CreatedAt = timeProvider.GetUtcNow().UtcDateTime
        };

        flag._variants.AddRange(variants);
        flag.RaiseDomainEvent(new FeatureFlagCreatedEvent(flag.Id.Value, flag.Key, flag.FlagType));
        return flag;
    }

    public void Update(string name, string? description, bool defaultEnabled, TimeProvider timeProvider)
    {
        List<string> changedProperties = new();

        if (Name != name)
        {
            changedProperties.Add(nameof(Name));
        }

        if (Description != description)
        {
            changedProperties.Add(nameof(Description));
        }

        if (DefaultEnabled != defaultEnabled)
        {
            changedProperties.Add(nameof(DefaultEnabled));
        }

        Name = name;
        Description = description;
        DefaultEnabled = defaultEnabled;
        UpdatedAt = timeProvider.GetUtcNow().UtcDateTime;

        string changedProps = string.Join(",", changedProperties);
        RaiseDomainEvent(new FeatureFlagUpdatedEvent(Id.Value, Key, changedProps));
    }

    public void UpdatePercentage(int percentage, TimeProvider timeProvider)
    {
        if (FlagType != FlagType.Percentage)
        {
            throw new FeatureFlagException("Can only update percentage on Percentage flags");
        }

        if (percentage < 0 || percentage > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(percentage));
        }

        RolloutPercentage = percentage;
        DefaultEnabled = percentage > 0;
        UpdatedAt = timeProvider.GetUtcNow().UtcDateTime;
    }

    public void SetVariants(IReadOnlyList<VariantWeight> variants, TimeProvider timeProvider)
    {
        if (FlagType != FlagType.Variant)
        {
            throw new FeatureFlagException("Can only set variants on Variant flags.");
        }

        if (variants.Count == 0)
        {
            throw new ArgumentException("At least one variant required.", nameof(variants));
        }

        if (DefaultVariant is not null && !variants.Any(v => v.Name == DefaultVariant))
        {
            throw new ArgumentException("Current default variant must be in the new variants list.", nameof(variants));
        }

        _variants.Clear();
        _variants.AddRange(variants);
        UpdatedAt = timeProvider.GetUtcNow().UtcDateTime;
    }

    public void MarkDeleted()
    {
        RaiseDomainEvent(new FeatureFlagDeletedEvent(Id.Value, Key));
    }
}
