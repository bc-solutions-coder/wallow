using Foundry.Storage.Infrastructure.Configuration;

namespace Foundry.Storage.Tests.Infrastructure;

public sealed class StorageOptionsTests
{
    [Fact]
    public void GetBucketForRegion_WithNoRegionOverride_ReturnsDefaultBucket()
    {
        S3StorageOptions options = new()
        {
            BucketName = "default-bucket",
            RegionBuckets = new Dictionary<string, string>()
        };

        string result = options.GetBucketForRegion("us-west-2");

        result.Should().Be("default-bucket");
    }

    [Fact]
    public void GetBucketForRegion_WithMatchingRegionOverride_ReturnsRegionBucket()
    {
        S3StorageOptions options = new()
        {
            BucketName = "default-bucket",
            RegionBuckets = new Dictionary<string, string>
            {
                ["eu-west-1"] = "eu-bucket",
                ["ap-southeast-1"] = "ap-bucket"
            }
        };

        string result = options.GetBucketForRegion("eu-west-1");

        result.Should().Be("eu-bucket");
    }

    [Fact]
    public void GetBucketForRegion_WithEmptyRegion_ReturnsDefaultBucket()
    {
        S3StorageOptions options = new()
        {
            BucketName = "default-bucket",
            RegionBuckets = new Dictionary<string, string>
            {
                ["us-east-1"] = "east-bucket"
            }
        };

        string result = options.GetBucketForRegion("");

        result.Should().Be("default-bucket");
    }

    [Fact]
    public void GetBucketForRegion_WithNullRegion_ThrowsArgumentNullException()
    {
        S3StorageOptions options = new()
        {
            BucketName = "default-bucket",
            RegionBuckets = new Dictionary<string, string>()
        };

        Action act = () => options.GetBucketForRegion(null!);

        act.Should().Throw<ArgumentNullException>();
    }
}
