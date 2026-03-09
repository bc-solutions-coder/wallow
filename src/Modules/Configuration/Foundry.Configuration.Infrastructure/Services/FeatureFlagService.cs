using System.Security.Cryptography;
using System.Text;
using Foundry.Configuration.Application.FeatureFlags.Contracts;
using Foundry.Configuration.Domain.Entities;
using Foundry.Configuration.Domain.Enums;
using Foundry.Configuration.Domain.Events;
using Foundry.Configuration.Domain.ValueObjects;
using Wolverine;

namespace Foundry.Configuration.Infrastructure.Services;

public sealed class FeatureFlagService(
    IFeatureFlagRepository repository,
    IMessageBus messageBus,
    TimeProvider timeProvider) : IFeatureFlagService
{

    public async Task<bool> IsEnabledAsync(string key, Guid tenantId, Guid? userId = null, CancellationToken ct = default)
    {
        FeatureFlag? flag = await repository.GetByKeyAsync(key, ct);
        if (flag is null)
        {
            return false;
        }

        FeatureFlagOverride? activeOverride = ResolveOverride(flag, tenantId, userId);

        bool result;
        string reason;

        if (activeOverride?.IsEnabled is not null)
        {
            result = activeOverride.IsEnabled.Value;
            reason = FormatOverrideReason(activeOverride);
        }
        else
        {
            result = flag.FlagType switch
            {
                FlagType.Percentage => EvaluatePercentage(key, userId, flag.RolloutPercentage ?? 0),
                _ => flag.DefaultEnabled
            };
            reason = flag.FlagType == FlagType.Percentage ? "Percentage rollout" : "Default value";
        }

        await PublishEvaluationAsync(key, tenantId, userId, result.ToString(), reason);
        return result;
    }

    public async Task<string?> GetVariantAsync(string key, Guid tenantId, Guid? userId = null, CancellationToken ct = default)
    {
        FeatureFlag? flag = await repository.GetByKeyAsync(key, ct);
        if (flag is null)
        {
            return null;
        }

        FeatureFlagOverride? activeOverride = ResolveOverride(flag, tenantId, userId);

        string? result;
        string reason;

        if (activeOverride?.Variant is not null)
        {
            result = activeOverride.Variant;
            reason = FormatOverrideReason(activeOverride);
        }
        else if (flag.FlagType == FlagType.Variant && flag.Variants.Count > 0)
        {
            result = SelectVariantByWeight(key, userId, flag.Variants);
            reason = "Weighted variant selection";
        }
        else
        {
            result = flag.DefaultVariant;
            reason = "Default variant";
        }

        await PublishEvaluationAsync(key, tenantId, userId, result ?? "null", reason);
        return result;
    }

    public async Task<Dictionary<string, object>> GetAllFlagsAsync(Guid tenantId, Guid? userId = null, CancellationToken ct = default)
    {
        IReadOnlyList<FeatureFlag> flags = await repository.GetAllAsync(ct);
        Dictionary<string, object> results = new(flags.Count);

        foreach (FeatureFlag flag in flags)
        {
            FeatureFlagOverride? activeOverride = ResolveOverride(flag, tenantId, userId);

            if (flag.FlagType == FlagType.Variant)
            {
                string? variant;
                string reason;

                if (activeOverride?.Variant is not null)
                {
                    variant = activeOverride.Variant;
                    reason = FormatOverrideReason(activeOverride);
                }
                else if (flag.Variants.Count > 0)
                {
                    variant = SelectVariantByWeight(flag.Key, userId, flag.Variants);
                    reason = "Weighted variant selection";
                }
                else
                {
                    variant = flag.DefaultVariant;
                    reason = "Default variant";
                }

                results[flag.Key] = variant ?? (object)false;
                await PublishEvaluationAsync(flag.Key, tenantId, userId, variant ?? "null", reason);
            }
            else
            {
                bool enabled;
                string reason;

                if (activeOverride?.IsEnabled is not null)
                {
                    enabled = activeOverride.IsEnabled.Value;
                    reason = FormatOverrideReason(activeOverride);
                }
                else
                {
                    enabled = flag.FlagType switch
                    {
                        FlagType.Percentage => EvaluatePercentage(flag.Key, userId, flag.RolloutPercentage ?? 0),
                        _ => flag.DefaultEnabled
                    };
                    reason = flag.FlagType == FlagType.Percentage ? "Percentage rollout" : "Default value";
                }

                results[flag.Key] = enabled;
                await PublishEvaluationAsync(flag.Key, tenantId, userId, enabled.ToString(), reason);
            }
        }

        return results;
    }

    private FeatureFlagOverride? ResolveOverride(FeatureFlag flag, Guid tenantId, Guid? userId)
    {
        List<FeatureFlagOverride> active = flag.Overrides.Where(o => !o.IsExpired(timeProvider)).ToList();

        // Priority: user+tenant > user-only > tenant-only > none
        if (userId.HasValue)
        {
            FeatureFlagOverride? userTenant = active.FirstOrDefault(
                o => o.UserId == userId && o.TenantId == tenantId);
            if (userTenant is not null)
            {
                return userTenant;
            }

            FeatureFlagOverride? userOnly = active.FirstOrDefault(
                o => o.UserId == userId && o.TenantId is null);
            if (userOnly is not null)
            {
                return userOnly;
            }
        }

        FeatureFlagOverride? tenantOnly = active.FirstOrDefault(
            o => o.TenantId == tenantId && o.UserId is null);

        return tenantOnly;
    }

#pragma warning disable CA5351 // MD5 used for consistent bucketing, not cryptographic security
    private static bool EvaluatePercentage(string flagKey, Guid? userId, int rolloutPercentage)
    {
        if (userId is null)
        {
            return rolloutPercentage > 0;
        }

        uint hash = BitConverter.ToUInt32(
            MD5.HashData(Encoding.UTF8.GetBytes(flagKey + userId)));

        return hash % 100 < (uint)rolloutPercentage;
    }

    private static string SelectVariantByWeight(
        string flagKey, Guid? userId, IReadOnlyCollection<VariantWeight> variants)
    {
        int totalWeight = variants.Sum(v => v.Weight);
        if (totalWeight <= 0)
        {
            return variants.First().Name;
        }

        uint hash = userId.HasValue
            ? BitConverter.ToUInt32(MD5.HashData(Encoding.UTF8.GetBytes(flagKey + userId)))
            : (uint)RandomNumberGenerator.GetInt32(int.MaxValue);

        int bucket = (int)(hash % (uint)totalWeight);

        foreach (VariantWeight variant in variants)
        {
            bucket -= variant.Weight;
            if (bucket < 0)
            {
                return variant.Name;
            }
        }

        return variants.Last().Name;
    }
#pragma warning restore CA5351

    private async Task PublishEvaluationAsync(
        string flagKey, Guid tenantId, Guid? userId, string result, string reason)
    {
        await messageBus.PublishAsync(new FeatureFlagEvaluatedEvent(
            flagKey, tenantId, userId, result, reason, DateTimeOffset.UtcNow));
    }

    private static string FormatOverrideReason(FeatureFlagOverride o) =>
        (o.TenantId, o.UserId) switch
        {
            (not null, not null) => "User+tenant override",
            (null, not null) => "User override",
            (not null, null) => "Tenant override",
            _ => "Override"
        };
}
