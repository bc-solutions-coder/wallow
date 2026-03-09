using Foundry.Configuration.Domain.Entities;
using Foundry.Configuration.Domain.Enums;
using Foundry.Configuration.Domain.Exceptions;
using Foundry.Configuration.Domain.ValueObjects;

namespace Foundry.Configuration.Tests.Domain;

public class FeatureFlagCreateBooleanTests
{
    [Fact]
    public void CreateBoolean_WithValidData_ReturnsBooleanFlag()
    {
        FeatureFlag flag = FeatureFlag.CreateBoolean("dark_mode", "Dark Mode", true, TimeProvider.System, "Enable dark mode");

        flag.Key.Should().Be("dark_mode");
        flag.Name.Should().Be("Dark Mode");
        flag.Description.Should().Be("Enable dark mode");
        flag.FlagType.Should().Be(FlagType.Boolean);
        flag.DefaultEnabled.Should().BeTrue();
        flag.RolloutPercentage.Should().BeNull();
        flag.Variants.Should().BeEmpty();
        flag.DefaultVariant.Should().BeNull();
        flag.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
        flag.UpdatedAt.Should().BeNull();
    }

    [Fact]
    public void CreateBoolean_WithDisabledDefault_ReturnsFlagWithDefaultDisabled()
    {
        FeatureFlag flag = FeatureFlag.CreateBoolean("beta_feature", "Beta Feature", false, TimeProvider.System);

        flag.DefaultEnabled.Should().BeFalse();
        flag.Description.Should().BeNull();
    }

    [Fact]
    public void CreateBoolean_GeneratesUniqueId()
    {
        FeatureFlag flag1 = FeatureFlag.CreateBoolean("key1", "Flag 1", true, TimeProvider.System);
        FeatureFlag flag2 = FeatureFlag.CreateBoolean("key2", "Flag 2", true, TimeProvider.System);

        flag1.Id.Should().NotBe(flag2.Id);
    }
}

public class FeatureFlagCreatePercentageTests
{
    [Fact]
    public void CreatePercentage_WithValidPercentage_ReturnsPercentageFlag()
    {
        FeatureFlag flag = FeatureFlag.CreatePercentage("rollout", "Gradual Rollout", 50, TimeProvider.System, "50% rollout");

        flag.Key.Should().Be("rollout");
        flag.Name.Should().Be("Gradual Rollout");
        flag.FlagType.Should().Be(FlagType.Percentage);
        flag.RolloutPercentage.Should().Be(50);
        flag.DefaultEnabled.Should().BeTrue();
    }

    [Fact]
    public void CreatePercentage_WithZeroPercent_SetsDefaultDisabled()
    {
        FeatureFlag flag = FeatureFlag.CreatePercentage("rollout", "Rollout", 0, TimeProvider.System);

        flag.RolloutPercentage.Should().Be(0);
        flag.DefaultEnabled.Should().BeFalse();
    }

    [Fact]
    public void CreatePercentage_WithHundredPercent_SetsDefaultEnabled()
    {
        FeatureFlag flag = FeatureFlag.CreatePercentage("rollout", "Rollout", 100, TimeProvider.System);

        flag.RolloutPercentage.Should().Be(100);
        flag.DefaultEnabled.Should().BeTrue();
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    [InlineData(-50)]
    [InlineData(200)]
    public void CreatePercentage_WithInvalidPercentage_ThrowsArgumentOutOfRange(int percentage)
    {
        Action act = () => FeatureFlag.CreatePercentage("rollout", "Rollout", percentage, TimeProvider.System);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}

public class FeatureFlagCreateVariantTests
{
    [Fact]
    public void CreateVariant_WithValidVariants_ReturnsVariantFlag()
    {
        List<VariantWeight> variants =
        [
            new("control", 50),
            new("treatment", 50)
        ];

        FeatureFlag flag = FeatureFlag.CreateVariant("ab_test", "A/B Test", variants, "control", TimeProvider.System, "Test description");

        flag.Key.Should().Be("ab_test");
        flag.Name.Should().Be("A/B Test");
        flag.FlagType.Should().Be(FlagType.Variant);
        flag.DefaultEnabled.Should().BeTrue();
        flag.Variants.Should().HaveCount(2);
        flag.DefaultVariant.Should().Be("control");
    }

    [Fact]
    public void CreateVariant_WithEmptyVariants_ThrowsArgumentException()
    {
        VariantWeight[] variants = [];

        Action act = () => FeatureFlag.CreateVariant("ab_test", "A/B Test", variants, "control", TimeProvider.System);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*At least one variant required*");
    }

    [Fact]
    public void CreateVariant_WithDefaultNotInList_ThrowsArgumentException()
    {
        List<VariantWeight> variants =
        [
            new("control", 50),
            new("treatment", 50)
        ];

        Action act = () => FeatureFlag.CreateVariant("ab_test", "A/B Test", variants, "missing", TimeProvider.System);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*Default variant must be in variants list*");
    }
}

public class FeatureFlagUpdateTests
{
    [Fact]
    public void Update_WithNewValues_ChangesNameDescriptionAndEnabled()
    {
        FeatureFlag flag = FeatureFlag.CreateBoolean("key", "Original", true, TimeProvider.System);

        flag.Update("Updated", "New description", false, TimeProvider.System);

        flag.Name.Should().Be("Updated");
        flag.Description.Should().Be("New description");
        flag.DefaultEnabled.Should().BeFalse();
        flag.UpdatedAt.Should().NotBeNull();
        flag.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Update_WhenCalled_SetsUpdatedAtTimestamp()
    {
        FeatureFlag flag = FeatureFlag.CreateBoolean("key", "Name", true, TimeProvider.System);
        flag.UpdatedAt.Should().BeNull();

        flag.Update("Name", null, true, TimeProvider.System);

        flag.UpdatedAt.Should().NotBeNull();
    }
}

public class FeatureFlagUpdatePercentageTests
{
    [Fact]
    public void UpdatePercentage_OnPercentageFlag_UpdatesValue()
    {
        FeatureFlag flag = FeatureFlag.CreatePercentage("rollout", "Rollout", 25, TimeProvider.System);

        flag.UpdatePercentage(75, TimeProvider.System);

        flag.RolloutPercentage.Should().Be(75);
        flag.DefaultEnabled.Should().BeTrue();
        flag.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void UpdatePercentage_ToZero_DisablesFlag()
    {
        FeatureFlag flag = FeatureFlag.CreatePercentage("rollout", "Rollout", 50, TimeProvider.System);

        flag.UpdatePercentage(0, TimeProvider.System);

        flag.RolloutPercentage.Should().Be(0);
        flag.DefaultEnabled.Should().BeFalse();
    }

    [Fact]
    public void UpdatePercentage_OnBooleanFlag_ThrowsInvalidOperation()
    {
        FeatureFlag flag = FeatureFlag.CreateBoolean("key", "Boolean Flag", true, TimeProvider.System);

        Action act = () => flag.UpdatePercentage(50, TimeProvider.System);

        act.Should().Throw<FeatureFlagException>()
            .WithMessage("*Percentage*");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void UpdatePercentage_WithInvalidValue_ThrowsArgumentOutOfRange(int percentage)
    {
        FeatureFlag flag = FeatureFlag.CreatePercentage("rollout", "Rollout", 50, TimeProvider.System);

        Action act = () => flag.UpdatePercentage(percentage, TimeProvider.System);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}

public class FeatureFlagSetVariantsTests
{
    [Fact]
    public void SetVariants_OnVariantFlag_ReplacesVariants()
    {
        List<VariantWeight> original =
        [
            new("control", 50),
            new("treatment", 50)
        ];
        FeatureFlag flag = FeatureFlag.CreateVariant("ab_test", "Test", original, "control", TimeProvider.System);

        List<VariantWeight> updated =
        [
            new("control", 33),
            new("treatment_a", 33),
            new("treatment_b", 34)
        ];

        flag.SetVariants(updated, TimeProvider.System);

        flag.Variants.Should().HaveCount(3);
        flag.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void SetVariants_WithEmptyList_ThrowsArgumentException()
    {
        List<VariantWeight> original = [new("control", 100)];
        FeatureFlag flag = FeatureFlag.CreateVariant("ab_test", "Test", original, "control", TimeProvider.System);

        Action act = () => flag.SetVariants([], TimeProvider.System);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*At least one variant required*");
    }

    [Fact]
    public void SetVariants_WithoutCurrentDefault_ThrowsArgumentException()
    {
        List<VariantWeight> original =
        [
            new("control", 50),
            new("treatment", 50)
        ];
        FeatureFlag flag = FeatureFlag.CreateVariant("ab_test", "Test", original, "control", TimeProvider.System);

        List<VariantWeight> updated = [new("new_variant", 100)];

        Action act = () => flag.SetVariants(updated, TimeProvider.System);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*Current default variant must be in the new variants list*");
    }

    [Fact]
    public void SetVariants_OnBooleanFlag_ThrowsInvalidOperation()
    {
        FeatureFlag flag = FeatureFlag.CreateBoolean("key", "Boolean", true, TimeProvider.System);

        Action act = () => flag.SetVariants([new("v1", 100)], TimeProvider.System);

        act.Should().Throw<FeatureFlagException>()
            .WithMessage("*Variant*");
    }
}

public class FeatureFlagOverridesCollectionTests
{
    [Fact]
    public void Overrides_WhenNewFlag_IsEmpty()
    {
        FeatureFlag flag = FeatureFlag.CreateBoolean("key", "Name", true, TimeProvider.System);

        flag.Overrides.Should().BeEmpty();
    }
}
