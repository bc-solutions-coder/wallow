using Foundry.Storage.Infrastructure.Configuration;
using Foundry.Storage.Infrastructure.Providers;
using Microsoft.Extensions.Options;

namespace Foundry.Storage.Tests.Infrastructure;

public sealed class LocalStorageProviderTests : IDisposable
{
    private readonly string _tempPath;
    private readonly LocalStorageProvider _provider;

    public LocalStorageProviderTests()
    {
        _tempPath = Path.Combine(Path.GetTempPath(), "foundry-storage-tests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempPath);

        IOptions<StorageOptions> options = Options.Create(new StorageOptions
        {
            Local = new LocalStorageOptions
            {
                BasePath = _tempPath,
                BaseUrl = "http://localhost:5000"
            }
        });

        _provider = new LocalStorageProvider(options);
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
        using MemoryStream stream = new MemoryStream(content);

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
        using MemoryStream stream = new MemoryStream([1, 2, 3]);

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
        using MemoryStream uploadStream = new MemoryStream(content);
        await _provider.UploadAsync(uploadStream, key, "text/plain");

        // Act
        await using Stream downloadStream = await _provider.DownloadAsync(key);
        using MemoryStream memoryStream = new MemoryStream();
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
        using MemoryStream stream = new MemoryStream([1, 2, 3]);
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
        using MemoryStream stream = new MemoryStream([1, 2, 3]);
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
    public async Task GetPresignedUrlAsync_ForDownload_ReturnsApiEndpoint()
    {
        // Arrange
        string key = "test/presigned.txt";

        // Act
        string url = await _provider.GetPresignedUrlAsync(key, TimeSpan.FromHours(1), forUpload: false);

        // Assert
        url.Should().StartWith("http://localhost:5000");
        url.Should().Contain("download");
        url.Should().Contain(Uri.EscapeDataString(key));
    }

    [Fact]
    public async Task GetPresignedUrlAsync_ForUpload_ReturnsApiEndpoint()
    {
        // Arrange
        string key = "test/upload-target.txt";

        // Act
        string url = await _provider.GetPresignedUrlAsync(key, TimeSpan.FromMinutes(15), forUpload: true);

        // Assert
        url.Should().StartWith("http://localhost:5000");
        url.Should().Contain("upload");
    }
}
