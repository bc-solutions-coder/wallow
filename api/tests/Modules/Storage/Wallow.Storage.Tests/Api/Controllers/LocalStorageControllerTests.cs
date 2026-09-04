using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Wallow.Storage.Api.Controllers;
using Wallow.Storage.Application.Services;
using Wallow.Storage.Infrastructure.Configuration;
using Wallow.Storage.Infrastructure.Providers;

namespace Wallow.Storage.Tests.Api.Controllers;

/// <summary>
/// Exercises the key-addressed endpoints LocalStorageProvider's presigned URLs point at,
/// against a real provider over a temp directory: possession of a valid signature is the
/// entire authorization model, so most of these tests are about rejecting bad signatures.
/// </summary>
public sealed class LocalStorageControllerTests : IDisposable
{
    private readonly string _tempPath;
    private readonly LocalPresignedUrlSigner _signer = new();
    private readonly LocalStorageProvider _provider;
    private readonly LocalStorageController _controller;

    public LocalStorageControllerTests()
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
        _controller = new LocalStorageController(_provider, _signer)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempPath))
        {
            Directory.Delete(_tempPath, recursive: true);
        }
    }

    private static long FutureExpiry => DateTimeOffset.UtcNow.AddMinutes(15).ToUnixTimeSeconds();

    private string SignDownload(string key, long expires) =>
        _signer.Sign(LocalPresignedUrlSigner.DownloadMethod, key, expires);

    private string SignUpload(string key, long expires) =>
        _signer.Sign(LocalPresignedUrlSigner.UploadMethod, key, expires);

    #region Download

    [Fact]
    public async Task Download_WithValidSignature_StreamsFileContent()
    {
        string key = "tenant-1/bucket/report.txt";
        byte[] content = "stored bytes"u8.ToArray();
        using MemoryStream upload = new(content);
        await _provider.UploadAsync(upload, key, "text/plain");
        long expires = FutureExpiry;

        IActionResult result = await _controller.Download(
            key, expires, SignDownload(key, expires), CancellationToken.None);

        FileStreamResult fileResult = result.Should().BeOfType<FileStreamResult>().Subject;
        fileResult.ContentType.Should().Be("text/plain");
        using MemoryStream downloaded = new();
        await fileResult.FileStream.CopyToAsync(downloaded);
        downloaded.ToArray().Should().BeEquivalentTo(content);
    }

    [Fact]
    public async Task Download_WithUnknownExtension_FallsBackToOctetStream()
    {
        string key = "tenant-1/bucket/blob.unknownext";
        using MemoryStream upload = new([1, 2, 3]);
        await _provider.UploadAsync(upload, key, "application/octet-stream");
        long expires = FutureExpiry;

        IActionResult result = await _controller.Download(
            key, expires, SignDownload(key, expires), CancellationToken.None);

        FileStreamResult fileResult = result.Should().BeOfType<FileStreamResult>().Subject;
        fileResult.ContentType.Should().Be("application/octet-stream");
        await fileResult.FileStream.DisposeAsync();
    }

    [Fact]
    public async Task Download_WithInvalidSignature_Returns403()
    {
        string key = "tenant-1/bucket/report.txt";
        using MemoryStream upload = new([1, 2, 3]);
        await _provider.UploadAsync(upload, key, "text/plain");

        IActionResult result = await _controller.Download(
            key, FutureExpiry, "forged-signature", CancellationToken.None);

        ObjectResult objectResult = result.Should().BeAssignableTo<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task Download_WithExpiredSignature_Returns403()
    {
        string key = "tenant-1/bucket/report.txt";
        using MemoryStream upload = new([1, 2, 3]);
        await _provider.UploadAsync(upload, key, "text/plain");
        long expires = DateTimeOffset.UtcNow.AddMinutes(-1).ToUnixTimeSeconds();

        IActionResult result = await _controller.Download(
            key, expires, SignDownload(key, expires), CancellationToken.None);

        ObjectResult objectResult = result.Should().BeAssignableTo<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task Download_WithSignatureForDifferentKey_Returns403()
    {
        string key = "tenant-1/bucket/report.txt";
        using MemoryStream upload = new([1, 2, 3]);
        await _provider.UploadAsync(upload, key, "text/plain");
        long expires = FutureExpiry;

        IActionResult result = await _controller.Download(
            key, expires, SignDownload("tenant-2/bucket/other.txt", expires), CancellationToken.None);

        ObjectResult objectResult = result.Should().BeAssignableTo<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task Download_WithUploadSignature_Returns403()
    {
        // An upload URL must not double as a download URL.
        string key = "tenant-1/bucket/report.txt";
        using MemoryStream upload = new([1, 2, 3]);
        await _provider.UploadAsync(upload, key, "text/plain");
        long expires = FutureExpiry;

        IActionResult result = await _controller.Download(
            key, expires, SignUpload(key, expires), CancellationToken.None);

        ObjectResult objectResult = result.Should().BeAssignableTo<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task Download_WhenFileDoesNotExist_Returns404()
    {
        string key = "tenant-1/bucket/missing.txt";
        long expires = FutureExpiry;

        IActionResult result = await _controller.Download(
            key, expires, SignDownload(key, expires), CancellationToken.None);

        ObjectResult objectResult = result.Should().BeAssignableTo<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    #endregion

    #region Upload

    [Fact]
    public async Task Upload_WithValidSignature_WritesRequestBodyToStorage()
    {
        string key = "tenant-1/bucket/incoming.txt";
        byte[] content = "uploaded bytes"u8.ToArray();
        _controller.HttpContext.Request.Body = new MemoryStream(content);
        _controller.HttpContext.Request.ContentType = "text/plain";
        long expires = FutureExpiry;

        IActionResult result = await _controller.Upload(
            key, expires, SignUpload(key, expires), CancellationToken.None);

        result.Should().BeOfType<OkResult>();
        await using Stream stored = await _provider.DownloadAsync(key);
        using MemoryStream storedContent = new();
        await stored.CopyToAsync(storedContent);
        storedContent.ToArray().Should().BeEquivalentTo(content);
    }

    [Fact]
    public async Task Upload_WithInvalidSignature_Returns403AndWritesNothing()
    {
        string key = "tenant-1/bucket/incoming.txt";
        _controller.HttpContext.Request.Body = new MemoryStream([1, 2, 3]);

        IActionResult result = await _controller.Upload(
            key, FutureExpiry, "forged-signature", CancellationToken.None);

        ObjectResult objectResult = result.Should().BeAssignableTo<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        (await _provider.ExistsAsync(key)).Should().BeFalse();
    }

    [Fact]
    public async Task Upload_WithDownloadSignature_Returns403AndWritesNothing()
    {
        // A download URL must not authorize a write.
        string key = "tenant-1/bucket/incoming.txt";
        _controller.HttpContext.Request.Body = new MemoryStream([1, 2, 3]);
        long expires = FutureExpiry;

        IActionResult result = await _controller.Upload(
            key, expires, SignDownload(key, expires), CancellationToken.None);

        ObjectResult objectResult = result.Should().BeAssignableTo<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        (await _provider.ExistsAsync(key)).Should().BeFalse();
    }

    [Fact]
    public async Task Upload_WithoutContentType_DefaultsToOctetStream()
    {
        string key = "tenant-1/bucket/raw.bin";
        _controller.HttpContext.Request.Body = new MemoryStream([1, 2, 3]);
        long expires = FutureExpiry;

        IActionResult result = await _controller.Upload(
            key, expires, SignUpload(key, expires), CancellationToken.None);

        result.Should().BeOfType<OkResult>();
        (await _provider.ExistsAsync(key)).Should().BeTrue();
    }

    #endregion
}
