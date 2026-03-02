using Foundry.Configuration.Application.FeatureFlags.DTOs;
using Foundry.Configuration.Application.FeatureFlags.Mappings;
using Foundry.Configuration.Domain.Entities;
using Foundry.Configuration.Domain.Enums;
using Foundry.Configuration.Domain.Identity;
using Foundry.Configuration.Domain.ValueObjects;

namespace Foundry.Configuration.Tests.Application.Mappings;

public class FeatureFlagMappingsTests
{
    [Fact]
    public void ToDto_BooleanFlag_MapsAllFields()
    {
        FeatureFlag flag = FeatureFlag.CreateBoolean("dark_mode", "Dark Mode", true, TimeProvider.System, "Enable dark mode");

        FeatureFlagDto dto = flag.ToDto();

        dto.Id.Should().Be(flag.Id.Value);
        dto.Key.Should().Be("dark_mode");
        dto.Name.Should().Be("Dark Mode");
        dto.Description.Should().Be("Enable dark mode");
        dto.FlagType.Should().Be(FlagType.Boolean);
        dto.DefaultEnabled.Should().BeTrue();
        dto.RolloutPercentage.Should().BeNull();
        dto.Variants.Should().BeNull();
        dto.DefaultVariant.Should().BeNull();
        dto.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void ToDto_PercentageFlag_MapsRolloutPercentage()
    {
        FeatureFlag flag = FeatureFlag.CreatePercentage("rollout", "Rollout", 50, TimeProvider.System);

        FeatureFlagDto dto = flag.ToDto();

        dto.FlagType.Should().Be(FlagType.Percentage);
        dto.RolloutPercentage.Should().Be(50);
    }

    [Fact]
    public void ToDto_VariantFlag_MapsVariantsAndDefaultVariant()
    {
        List<VariantWeight> variants =
        [
            new VariantWeight("control", 50),
            new VariantWeight("treatment", 50)
        ];
        FeatureFlag flag = FeatureFlag.CreateVariant("ab_test", "A/B Test", variants, "control", TimeProvider.System);

        FeatureFlagDto dto = flag.ToDto();

        dto.FlagType.Should().Be(FlagType.Variant);
        dto.DefaultVariant.Should().Be("control");
        dto.Variants.Should().HaveCount(2);
        dto.Variants![0].Name.Should().Be("control");
        dto.Variants![0].Weight.Should().Be(50);
        dto.Variants![1].Name.Should().Be("treatment");
        dto.Variants![1].Weight.Should().Be(50);
    }

    [Fact]
    public void ToDto_Override_MapsAllFields()
    {
        FeatureFlagId flagId = FeatureFlagId.New();
        Guid tenantId = Guid.NewGuid();
        DateTime expiresAt = DateTime.UtcNow.AddDays(7);
        FeatureFlagOverride over = FeatureFlagOverride.CreateForTenant(flagId, tenantId, true, TimeProvider.System, "variant_a", expiresAt);

        FeatureFlagOverrideDto dto = over.ToDto();

        dto.Id.Should().Be(over.Id.Value);
        dto.FlagId.Should().Be(flagId.Value);
        dto.TenantId.Should().Be(tenantId);
        dto.UserId.Should().BeNull();
        dto.IsEnabled.Should().BeTrue();
        dto.Variant.Should().Be("variant_a");
        dto.ExpiresAt.Should().Be(expiresAt);
        dto.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void ToDto_UserOverride_MapsUserId()
    {
        FeatureFlagId flagId = FeatureFlagId.New();
        Guid userId = Guid.NewGuid();
        FeatureFlagOverride over = FeatureFlagOverride.CreateForUser(flagId, userId, false, TimeProvider.System);

        FeatureFlagOverrideDto dto = over.ToDto();

        dto.TenantId.Should().BeNull();
        dto.UserId.Should().Be(userId);
        dto.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void ToDto_TenantUserOverride_MapsBothIds()
    {
        FeatureFlagId flagId = FeatureFlagId.New();
        Guid tenantId = Guid.NewGuid();
        Guid userId = Guid.NewGuid();
        FeatureFlagOverride over = FeatureFlagOverride.CreateForTenantUser(flagId, tenantId, userId, true, TimeProvider.System);

        FeatureFlagOverrideDto dto = over.ToDto();

        dto.TenantId.Should().Be(tenantId);
        dto.UserId.Should().Be(userId);
    }
}
