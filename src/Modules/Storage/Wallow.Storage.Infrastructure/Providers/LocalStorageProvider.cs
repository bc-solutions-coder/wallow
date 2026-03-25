using Microsoft.Extensions.Options;
using Wallow.Shared.Contracts.Storage;
using Wallow.Storage.Infrastructure.Configuration;

namespace Wallow.Storage.Infrastructure.Providers;

/// <summary>
/// Local filesystem storage provider for development environments.
/// </summary>
public sealed class LocalStorageProvider(IOptions<StorageOptions> options) : IStorageProvider
{
    private readonly LocalStorageOptions _options = options.Value.Local;

    public async Task<string> UploadAsync(Stream content, string key, string contentType, CancellationToken ct = default)
    {
        string filePath = GetFilePath(key);
        string? directory = Path.GetDirectoryName(filePath);

        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using FileStream fileStream = new(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
        await content.CopyToAsync(fileStream, ct);

        // Return file hash as ETag equivalent
        return Convert.ToBase64String(BitConverter.GetBytes(DateTime.UtcNow.Ticks));
    }

    public Task<Stream> DownloadAsync(string key, CancellationToken ct = default)
    {
        string filePath = GetFilePath(key);

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"File not found: {key}", key);
        }

        FileStream stream = new(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Task.FromResult<Stream>(stream);
    }

    public Task DeleteAsync(string key, CancellationToken ct = default)
    {
        string filePath = GetFilePath(key);

        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }

        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string key, CancellationToken ct = default)
    {
        string filePath = GetFilePath(key);
        return Task.FromResult(File.Exists(filePath));
    }

    public Task<string> GetPresignedUrlAsync(string key, TimeSpan expiry, bool forUpload = false, CancellationToken ct = default)
    {
        // Local storage doesn't support true presigned URLs
        // Return an API endpoint URL that can be used to download/upload
        string baseUrl = _options.BaseUrl?.TrimEnd('/') ?? "http://localhost:5000";

        if (forUpload)
        {
            // For uploads, return the upload endpoint
            return Task.FromResult($"{baseUrl}/api/storage/upload?key={Uri.EscapeDataString(key)}");
        }

        // For downloads, return the download endpoint
        return Task.FromResult($"{baseUrl}/api/storage/files/download?key={Uri.EscapeDataString(key)}");
    }

    private string GetFilePath(string key)
    {
        // Normalize path separators and combine with base path
        string normalizedKey = key.Replace('/', Path.DirectorySeparatorChar);
        string filePath = Path.GetFullPath(Path.Combine(_options.BasePath, normalizedKey));
        string baseDirectory = Path.GetFullPath(_options.BasePath);

        if (!filePath.StartsWith(baseDirectory, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Path traversal detected: key '{key}' resolves outside the storage base directory.");
        }

        return filePath;
    }
}
