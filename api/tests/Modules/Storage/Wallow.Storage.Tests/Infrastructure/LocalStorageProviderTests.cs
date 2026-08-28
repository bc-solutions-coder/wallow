using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Wallow.Shared.Contracts.Storage;
using Wallow.Storage.Application.Services;
using Wallow.Storage.Infrastructure.Configuration;
using Wallow.Storage.Infrastructure.Providers;

namespace Wallow.Storage.Tests.Infrastructure;

public sealed class LocalStorageProviderTests : IDisposable
{
    private readonly string _tempPath;
    private readonly LocalPresignedUrlSigner _signer = new();
    private readonly LocalStorageProvider _provider;

    public LocalStorageProviderTests()
    {
        _tempPath = Path.Combine(Path.GetTempPath(), "wallow-storage-tests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempPath);

        IOptions<StorageOptions> options = Options.Create(new StorageOptions
        {
            Local = new LocalStorageOptions
            {
                BasePath = _tempPath,
                BaseUrl = "http://localhost:5001"
            }
        });

        _provider = new LocalStorageProvider(options, _signer);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempPath))
        {
            Directory.Delete(_tempPath, recursive: true);
        }
    }

    [Fact]
    public async Task UploadAsync_CreatesFileOnDisk()
    {
        // Arrange
        string key = "test-tenant/bucket/test-file.txt";
        byte[] content = "Hello, World!"u8.ToArray();
        using MemoryStream stream = new(content);

        // Act
        string etag = await _provider.UploadAsync(stream, key, "text/plain");

        // Assert
        etag.Should().NotBeNullOrEmpty();
        string filePath = Path.Combine(_tempPath, key.Replace('/', Path.DirectorySeparatorChar));
        File.Exists(filePath).Should().BeTrue();
        byte[] savedContent = await File.ReadAllBytesAsync(filePath);
        savedContent.Should().BeEquivalentTo(content);
    }

    [Fact]
    public async Task UploadAsync_CreatesNestedDirectories()
    {
        // Arrange
        string key = "tenant-123/invoices/2024/02/invoice.pdf";
        using MemoryStream stream = new([1, 2, 3]);

        // Act
        await _provider.UploadAsync(stream, key, "application/pdf");

        // Assert
        string filePath = Path.Combine(_tempPath, key.Replace('/', Path.DirectorySeparatorChar));
        File.Exists(filePath).Should().BeTrue();
    }

    [Fact]
    public async Task DownloadAsync_ReturnsFileContent()
    {
        // Arrange
        string key = "test/download.txt";
        byte[] content = "Download test content"u8.ToArray();
        using MemoryStream uploadStream = new(content);
        await _provider.UploadAsync(uploadStream, key, "text/plain");

        // Act
        await using Stream downloadStream = await _provider.DownloadAsync(key);
        using MemoryStream memoryStream = new();
        await downloadStream.CopyToAsync(memoryStream);

        // Assert
        memoryStream.ToArray().Should().BeEquivalentTo(content);
    }

    [Fact]
    public async Task DownloadAsync_WhenFileNotFound_ThrowsException()
    {
        // Arrange
        string key = "non-existent/file.txt";

        // Act
        Func<Task<Stream>> act = () => _provider.DownloadAsync(key);

        // Assert
        await act.Should().ThrowAsync<FileNotFoundException>();
    }

    [Fact]
    public async Task DeleteAsync_RemovesFile()
    {
        // Arrange
        string key = "test/delete.txt";
        using MemoryStream stream = new([1, 2, 3]);
        await _provider.UploadAsync(stream, key, "text/plain");

        string filePath = Path.Combine(_tempPath, key.Replace('/', Path.DirectorySeparatorChar));
        File.Exists(filePath).Should().BeTrue();

        // Act
        await _provider.DeleteAsync(key);

        // Assert
        File.Exists(filePath).Should().BeFalse();
    }

    [Fact]
    public async Task DeleteAsync_WhenFileNotFound_DoesNotThrow()
    {
        // Arrange
        string key = "non-existent/file.txt";

        // Act
        Func<Task> act = () => _provider.DeleteAsync(key);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ExistsAsync_WhenFileExists_ReturnsTrue()
    {
        // Arrange
        string key = "test/exists.txt";
        using MemoryStream stream = new([1, 2, 3]);
        await _provider.UploadAsync(stream, key, "text/plain");

        // Act
        bool exists = await _provider.ExistsAsync(key);

        // Assert
        exists.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsAsync_WhenFileNotExists_ReturnsFalse()
    {
        // Arrange
        string key = "non-existent/file.txt";

        // Act
        bool exists = await _provider.ExistsAsync(key);

        // Assert
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task GetPresignedUrlAsync_ForDownload_MintsSignedLocalEndpointUrl()
    {
        // Arrange
        string key = "test/presigned.txt";

        // Act
        string url = await _provider.GetPresignedUrlAsync(key, TimeSpan.FromHours(1), forUpload: false);

        // Assert -- the URL targets the key-addressed local endpoint and carries a
        // signature the process-wide signer accepts for a GET of exactly this key.
        url.Should().StartWith("http://localhost:5001/v1/storage/local/files?");
        (string parsedKey, long expires, string signature) = ParsePresignedQuery(url);
        parsedKey.Should().Be(key);
        _signer.Validate(LocalPresignedUrlSigner.DownloadMethod, key, expires, signature)
            .Should().BeTrue();
    }

    [Fact]
    public async Task GetPresignedUrlAsync_ForUpload_MintsPutSignedLocalEndpointUrl()
    {
        // Arrange
        string key = "test/upload-target.txt";

        // Act
        string url = await _provider.GetPresignedUrlAsync(key, TimeSpan.FromMinutes(15), forUpload: true);

        // Assert -- upload URLs are signed for PUT, and only PUT.
        url.Should().StartWith("http://localhost:5001/v1/storage/local/files?");
        (string parsedKey, long expires, string signature) = ParsePresignedQuery(url);
        parsedKey.Should().Be(key);
        _signer.Validate(LocalPresignedUrlSigner.UploadMethod, key, expires, signature)
            .Should().BeTrue();
        _signer.Validate(LocalPresignedUrlSigner.DownloadMethod, key, expires, signature)
            .Should().BeFalse();
    }

    [Fact]
    public async Task GetPresignedUrlAsync_EmbedsExpiryDerivedFromRequestedLifetime()
    {
        DateTimeOffset before = DateTimeOffset.UtcNow;
        string url = await _provider.GetPresignedUrlAsync("test/file.txt", TimeSpan.FromMinutes(15), forUpload: false);
        DateTimeOffset after = DateTimeOffset.UtcNow;

        (_, long expires, _) = ParsePresignedQuery(url);

        expires.Should().BeInRange(
            before.AddMinutes(15).ToUnixTimeSeconds(),
            after.AddMinutes(15).ToUnixTimeSeconds());
    }

    private static (string Key, long Expires, string Signature) ParsePresignedQuery(string url)
    {
        Dictionary<string, StringValues> query = QueryHelpers.ParseQuery(new Uri(url).Query);
        return (
            query["key"].ToString(),
            long.Parse(query["expires"].ToString(), System.Globalization.CultureInfo.InvariantCulture),
            query["sig"].ToString());
    }

    [Fact]
    public void GetFilePath_WithPathTraversal_ThrowsInvalidOperationException()
    {
        string key = "../../etc/passwd";

        Func<Task> act = () => _provider.DownloadAsync(key);

        act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Path traversal detected*");
    }

    [Fact]
    public async Task GetPresignedUrlAsync_WithNullBaseUrl_UsesDefaultUrl()
    {
        IOptions<StorageOptions> options = Options.Create(new StorageOptions
        {
            Local = new LocalStorageOptions
            {
                BasePath = _tempPath,
                BaseUrl = null
            }
        });
        LocalStorageProvider provider = new(options, _signer);

        string url = await provider.GetPresignedUrlAsync("test/file.txt", TimeSpan.FromHours(1));

        url.Should().StartWith("http://localhost:5001/v1/storage/local/files?");
    }

    [Fact]
    public async Task UploadAsync_WhenDirectoryAlreadyExists_StillWritesFile()
    {
        string key = "existing-dir/file.txt";
        string dirPath = Path.Combine(_tempPath, "existing-dir");
        Directory.CreateDirectory(dirPath);

        byte[] content = "test content"u8.ToArray();
        using MemoryStream stream = new(content);

        string etag = await _provider.UploadAsync(stream, key, "text/plain");

        etag.Should().NotBeNullOrEmpty();
        string filePath = Path.Combine(_tempPath, key.Replace('/', Path.DirectorySeparatorChar));
        File.Exists(filePath).Should().BeTrue();
    }

    [Fact]
    public async Task UploadAsync_OverwritesExistingFile()
    {
        string key = "test/overwrite.txt";
        using MemoryStream firstStream = new("first"u8.ToArray());
        await _provider.UploadAsync(firstStream, key, "text/plain");

        byte[] newContent = "second"u8.ToArray();
        using MemoryStream secondStream = new(newContent);
        await _provider.UploadAsync(secondStream, key, "text/plain");

        string filePath = Path.Combine(_tempPath, key.Replace('/', Path.DirectorySeparatorChar));
        byte[] savedContent = await File.ReadAllBytesAsync(filePath);
        savedContent.Should().BeEquivalentTo(newContent);
    }

    [Fact]
    public async Task GetPresignedUrlAsync_WithTrailingSlashBaseUrl_NormalizesUrl()
    {
        IOptions<StorageOptions> options = Options.Create(new StorageOptions
        {
            Local = new LocalStorageOptions
            {
                BasePath = _tempPath,
                BaseUrl = "http://localhost:5001/"
            }
        });
        LocalStorageProvider provider = new(options, _signer);

        string url = await provider.GetPresignedUrlAsync("test/file.txt", TimeSpan.FromHours(1));

        url.Should().NotContain("//v1");
        url.Should().StartWith("http://localhost:5001/v1/storage/local/files?");
    }

    [Fact]
    public async Task ListAsync_ReturnsKeysUnderPrefix_WithForwardSlashesAndLastModified()
    {
        using MemoryStream contentA = new("a"u8.ToArray());
        using MemoryStream contentB = new("b"u8.ToArray());
        using MemoryStream contentC = new("c"u8.ToArray());
        await _provider.UploadAsync(contentA, "tenant-1/bucket/a.txt", "text/plain");
        await _provider.UploadAsync(contentB, "tenant-1/bucket/nested/b.txt", "text/plain");
        await _provider.UploadAsync(contentC, "client-logos/client-1/logo.png", "image/png");

        List<StorageObjectInfo> objects = await _provider.ListAsync("tenant-").ToListAsync();

        objects.Select(o => o.Key).Should().BeEquivalentTo(
            "tenant-1/bucket/a.txt",
            "tenant-1/bucket/nested/b.txt");
        objects.Should().OnlyContain(o => o.LastModified > DateTimeOffset.UtcNow.AddMinutes(-5));
    }

    [Fact]
    public async Task ListAsync_ReturnsEmpty_WhenBasePathDoesNotExist()
    {
        IOptions<StorageOptions> options = Options.Create(new StorageOptions
        {
            Local = new LocalStorageOptions
            {
                BasePath = Path.Combine(_tempPath, "does-not-exist"),
                BaseUrl = "http://localhost:5001"
            }
        });
        LocalStorageProvider provider = new(options, _signer);

        List<StorageObjectInfo> objects = await provider.ListAsync("tenant-").ToListAsync();

        objects.Should().BeEmpty();
    }
}
