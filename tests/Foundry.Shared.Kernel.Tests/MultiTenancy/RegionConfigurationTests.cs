using Foundry.Shared.Kernel.MultiTenancy;

namespace Foundry.Shared.Kernel.Tests.MultiTenancy;

public class RegionConfigurationTests
{
    [Fact]
    public void UsEast_HasExpectedValue()
    {
        RegionConfiguration.UsEast.Should().Be("us-east-1");
    }

    [Fact]
    public void EuWest_HasExpectedValue()
    {
        RegionConfiguration.EuWest.Should().Be("eu-west-1");
    }

    [Fact]
    public void ApSoutheast_HasExpectedValue()
    {
        RegionConfiguration.ApSoutheast.Should().Be("ap-southeast-1");
    }

    [Fact]
    public void PrimaryRegion_IsUsEast()
    {
        RegionConfiguration.PrimaryRegion.Should().Be(RegionConfiguration.UsEast);
    }

    [Fact]
    public void AllRegionConstants_AreUnique()
    {
        string[] regions = [RegionConfiguration.UsEast, RegionConfiguration.EuWest, RegionConfiguration.ApSoutheast];

        regions.Should().OnlyHaveUniqueItems();
    }
}

public class RegionSettingsTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        RegionSettings settings = new RegionSettings("us-east-1", true, true);

        settings.Name.Should().Be("us-east-1");
        settings.IsPrimary.Should().BeTrue();
        settings.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Constructor_WithInactiveNonPrimary_SetsPropertiesCorrectly()
    {
        RegionSettings settings = new RegionSettings("eu-west-1", false, false);

        settings.Name.Should().Be("eu-west-1");
        settings.IsPrimary.Should().BeFalse();
        settings.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Equality_WithSameValues_AreEqual()
    {
        RegionSettings settings1 = new RegionSettings("us-east-1", true, true);
        RegionSettings settings2 = new RegionSettings("us-east-1", true, true);

        settings1.Should().Be(settings2);
    }

    [Fact]
    public void Equality_WithDifferentValues_AreNotEqual()
    {
        RegionSettings settings1 = new RegionSettings("us-east-1", true, true);
        RegionSettings settings2 = new RegionSettings("eu-west-1", false, true);

        settings1.Should().NotBe(settings2);
    }
}
