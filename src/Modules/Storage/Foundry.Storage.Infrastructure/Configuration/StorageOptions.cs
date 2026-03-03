using Foundry.Storage.Domain.Enums;

namespace Foundry.Storage.Infrastructure.Configuration;

public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    public StorageProvider Provider { get; set; } = StorageProvider.Local;
    public LocalStorageOptions Local { get; set; } = new();
    public S3StorageOptions S3 { get; set; } = new();
    public string ClamAvHost { get; set; } = "localhost";
    public int ClamAvPort { get; set; } = 3310;
}

public sealed class LocalStorageOptions
{
    public string BasePath { get; set; } = "/var/foundry/storage";
    public string? BaseUrl { get; set; }
}

public sealed class S3StorageOptions
{
    public string Endpoint { get; set; } = string.Empty;
    public string AccessKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public string BucketName { get; set; } = string.Empty;
    public bool UsePathStyle { get; set; } = true;
    public string Region { get; set; } = "us-east-1";

    /// <summary>
    /// Region-specific bucket overrides. Key = region name, Value = bucket name.
    /// Falls back to <see cref="BucketName"/> if no region-specific bucket is configured.
    /// </summary>
    public Dictionary<string, string> RegionBuckets { get; set; } = new();

    public string GetBucketForRegion(string region)
    {
        return RegionBuckets.TryGetValue(region, out string? bucket) ? bucket : BucketName;
    }
}
