namespace Wallow.Shared.Contracts.Storage;

/// <summary>
/// Low-level storage backend interface. Implementations handle actual file I/O.
/// </summary>
public interface IStorageProvider
{
    /// <summary>
    /// Upload content to the storage backend.
    /// </summary>
    /// <returns>The ETag or version identifier of the uploaded content.</returns>
    Task<string> UploadAsync(Stream content, string key, string contentType, CancellationToken ct = default);

    /// <summary>
    /// Download content from the storage backend.
    /// </summary>
    Task<Stream> DownloadAsync(string key, CancellationToken ct = default);

    /// <summary>
    /// Delete a file from the storage backend.
    /// </summary>
    Task DeleteAsync(string key, CancellationToken ct = default);

    /// <summary>
    /// Check if a file exists in the storage backend.
    /// </summary>
    Task<bool> ExistsAsync(string key, CancellationToken ct = default);

    /// <summary>
    /// Generate a presigned URL for direct access to the file.
    /// </summary>
    /// <param name="key">The storage key.</param>
    /// <param name="expiry">How long the URL should be valid.</param>
    /// <param name="forUpload">If true, generate a URL for uploading; otherwise for downloading.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<string> GetPresignedUrlAsync(string key, TimeSpan expiry, bool forUpload = false, CancellationToken ct = default);
}
