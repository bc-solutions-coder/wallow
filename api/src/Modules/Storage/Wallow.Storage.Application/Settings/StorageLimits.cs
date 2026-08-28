namespace Wallow.Storage.Application.Settings;

/// <summary>
/// The tenant-level upload limits resolved from the storage settings: maximum upload size,
/// allowed file extensions, and the total storage quota.
/// </summary>
public sealed class StorageLimits
{
    private const long BytesPerMegabyte = 1024L * 1024;
    private const string Wildcard = "*";

    // Null means every extension is allowed (the "*" / blank-list configuration).
    private readonly IReadOnlySet<string>? _allowedExtensions;

    private StorageLimits(long maxUploadSizeBytes, long quotaBytes, IReadOnlySet<string>? allowedExtensions)
    {
        MaxUploadSizeBytes = maxUploadSizeBytes;
        QuotaBytes = quotaBytes;
        _allowedExtensions = allowedExtensions;
    }

    public long MaxUploadSizeBytes { get; }

    public long QuotaBytes { get; }

    public static StorageLimits Create(int maxUploadSizeMb, string allowedFileTypes, int quotaMb)
    {
        long maxUploadSizeBytes = maxUploadSizeMb * BytesPerMegabyte;
        long quotaBytes = quotaMb * BytesPerMegabyte;

        HashSet<string> extensions = new(StringComparer.Ordinal);
        foreach (string entry in allowedFileTypes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string normalized = Normalize(entry);
            if (normalized == Wildcard)
            {
                return new StorageLimits(maxUploadSizeBytes, quotaBytes, allowedExtensions: null);
            }

            if (normalized.Length > 0)
            {
                extensions.Add(normalized);
            }
        }

        return new StorageLimits(
            maxUploadSizeBytes,
            quotaBytes,
            extensions.Count == 0 ? null : extensions);
    }

    public bool IsExtensionAllowed(string extension)
    {
        if (_allowedExtensions is null)
        {
            return true;
        }

        string normalized = Normalize(extension);
        return normalized.Length > 0 && _allowedExtensions.Contains(normalized);
    }

    private static string Normalize(string extension)
    {
        return extension.Trim().TrimStart('.').ToUpperInvariant();
    }
}
