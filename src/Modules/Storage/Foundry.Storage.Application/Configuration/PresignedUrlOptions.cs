namespace Foundry.Storage.Application.Configuration;

public sealed class PresignedUrlOptions
{
    public const string SectionName = "Storage:PresignedUrls";

    /// <summary>
    /// Maximum allowed expiry for download presigned URLs. Caller-supplied values are clamped to this ceiling.
    /// </summary>
    public int MaxDownloadExpiryMinutes { get; set; } = 60;

    /// <summary>
    /// Maximum allowed expiry for upload presigned URLs. Caller-supplied values are clamped to this ceiling.
    /// </summary>
    public int MaxUploadExpiryMinutes { get; set; } = 15;
}
