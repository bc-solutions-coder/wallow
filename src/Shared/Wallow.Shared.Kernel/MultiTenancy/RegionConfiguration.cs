namespace Wallow.Shared.Kernel.MultiTenancy;

public static class RegionConfiguration
{
    public const string UsEast = "us-east-1";
    public const string EuWest = "eu-west-1";
    public const string ApSoutheast = "ap-southeast-1";

    public const string PrimaryRegion = UsEast;
}

public record RegionSettings(string Name, bool IsPrimary, bool IsActive);
