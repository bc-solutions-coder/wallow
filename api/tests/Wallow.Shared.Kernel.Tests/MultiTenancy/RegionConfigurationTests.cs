using Wallow.Shared.Kernel.MultiTenancy;

namespace Wallow.Shared.Kernel.Tests.MultiTenancy;

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
