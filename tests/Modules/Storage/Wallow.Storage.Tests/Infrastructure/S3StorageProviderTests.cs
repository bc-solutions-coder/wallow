using System.Net;
using Amazon.S3;
using Amazon.S3.Model;
using Wallow.Shared.Kernel.MultiTenancy;
using Wallow.Storage.Infrastructure.Configuration;
using Wallow.Storage.Infrastructure.Providers;
using Microsoft.Extensions.Options;

namespace Wallow.Storage.Tests.Infrastructure;

public sealed class S3StorageProviderTests : IDisposable
{
    private readonly IAmazonS3 _mockS3Client;
    private readonly S3StorageProvider _provider;
    private const string TestBucket = "test-bucket";
    private const string TestEndpoint = "http://localhost:9000";
    private const string TestRegion = "us-east-1";

    public S3StorageProviderTests()
    {
        _mockS3Client = Substitute.For<IAmazonS3>();

        IOptions<StorageOptions> options = Options.Create(new StorageOptions
        {
            S3 = new S3StorageOptions
            {
                Endpoint = TestEndpoint,
                AccessKey = "test-access-key",
                SecretKey = "test-secret-key",
                BucketName = TestBucket,
                UsePathStyle = true,
                Region = TestRegion
            }
        });

        ITenantContext tenantContext = Substitute.For<ITenantContext>();
        tenantContext.Region.Returns(RegionConfiguration.PrimaryRegion);

        _provider = new S3StorageProvider(_mockS3Client, options, tenantContext);
    }

    [Fact]
    public async Task UploadAsync_SendsPutObjectRequest()
    {
        // Arrange
        string key = "test-tenant/bucket/test-file.txt";
        byte[] content = "Hello, World!"u8.ToArray();
        using MemoryStream stream = new(content);
        string etag = "\"abc123\"";

        _mockS3Client.PutObjectAsync(
            Arg.Is<PutObjectRequest>(r =>
                r.BucketName == TestBucket &&
                r.Key == key &&
                r.ContentType == "text/plain"),
            Arg.Any<CancellationToken>())
            .Returns(_ => new PutObjectResponse { ETag = etag });

        // Act
        string result = await _provider.UploadAsync(stream, key, "text/plain");

        // Assert
        result.Should().Be(etag);
        await _mockS3Client.Received(1).PutObjectAsync(
            Arg.Any<PutObjectRequest>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UploadAsync_PassesCorrectContentType()
    {
        // Arrange
        string key = "images/photo.jpg";
        using MemoryStream stream = new([1, 2, 3]);

        _mockS3Client.PutObjectAsync(
            Arg.Any<PutObjectRequest>(),
            Arg.Any<CancellationToken>())
            .Returns(_ => new PutObjectResponse { ETag = "\"xyz\"" });

        // Act
        await _provider.UploadAsync(stream, key, "image/jpeg");

        // Assert
        await _mockS3Client.Received(1).PutObjectAsync(
            Arg.Is<PutObjectRequest>(r => r.ContentType == "image/jpeg"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DownloadAsync_ReturnsStream()
    {
        // Arrange
        string key = "test/download.txt";
        byte[] content = "Download test content"u8.ToArray();
        MemoryStream responseStream = new(content);
        using GetObjectResponse getObjectResponse = new() { ResponseStream = responseStream };

        _mockS3Client.GetObjectAsync(
            Arg.Is<GetObjectRequest>(r =>
                r.BucketName == TestBucket &&
                r.Key == key),
            Arg.Any<CancellationToken>())
            .Returns(getObjectResponse);

        // Act
        Stream downloadStream = await _provider.DownloadAsync(key);
        using MemoryStream memoryStream = new();
        await downloadStream.CopyToAsync(memoryStream);

        // Assert
        memoryStream.ToArray().Should().BeEquivalentTo(content);
        await _mockS3Client.Received(1).GetObjectAsync(
            Arg.Any<GetObjectRequest>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DownloadAsync_WhenFileNotFound_ThrowsException()
    {
        // Arrange
        string key = "non-existent/file.txt";
        AmazonS3Exception s3Exception = new("Not found")
        {
            StatusCode = HttpStatusCode.NotFound
        };

        _mockS3Client.GetObjectAsync(
            Arg.Any<GetObjectRequest>(),
            Arg.Any<CancellationToken>())
            .Returns<GetObjectResponse>(_ => throw s3Exception);

        // Act
        Func<Task<Stream>> act = () => _provider.DownloadAsync(key);

        // Assert
        await act.Should().ThrowAsync<AmazonS3Exception>()
            .Where(ex => ex.StatusCode == HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteAsync_SendsDeleteObjectRequest()
    {
        // Arrange
        string key = "test/delete.txt";

        _mockS3Client.DeleteObjectAsync(
            Arg.Is<DeleteObjectRequest>(r =>
                r.BucketName == TestBucket &&
                r.Key == key),
            Arg.Any<CancellationToken>())
            .Returns(_ => new DeleteObjectResponse());

        // Act
        await _provider.DeleteAsync(key);

        // Assert
        await _mockS3Client.Received(1).DeleteObjectAsync(
            Arg.Any<DeleteObjectRequest>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteAsync_WhenFileNotFound_DoesNotThrow()
    {
        // Arrange
        string key = "non-existent/file.txt";

        _mockS3Client.DeleteObjectAsync(
            Arg.Any<DeleteObjectRequest>(),
            Arg.Any<CancellationToken>())
            .Returns(_ => new DeleteObjectResponse());

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

        _mockS3Client.GetObjectMetadataAsync(
            Arg.Is<GetObjectMetadataRequest>(r =>
                r.BucketName == TestBucket &&
                r.Key == key),
            Arg.Any<CancellationToken>())
            .Returns(_ => new GetObjectMetadataResponse());

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
        AmazonS3Exception s3Exception = new("Not found")
        {
            StatusCode = HttpStatusCode.NotFound
        };

        _mockS3Client.GetObjectMetadataAsync(
            Arg.Any<GetObjectMetadataRequest>(),
            Arg.Any<CancellationToken>())
            .Returns<GetObjectMetadataResponse>(_ => throw s3Exception);

        // Act
        bool exists = await _provider.ExistsAsync(key);

        // Assert
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task ExistsAsync_WhenOtherException_Throws()
    {
        // Arrange
        string key = "test/error.txt";
        AmazonS3Exception s3Exception = new("Internal server error")
        {
            StatusCode = HttpStatusCode.InternalServerError
        };

        _mockS3Client.GetObjectMetadataAsync(
            Arg.Any<GetObjectMetadataRequest>(),
            Arg.Any<CancellationToken>())
            .Returns<GetObjectMetadataResponse>(_ => throw s3Exception);

        // Act
        Func<Task<bool>> act = () => _provider.ExistsAsync(key);

        // Assert
        await act.Should().ThrowAsync<AmazonS3Exception>()
            .Where(ex => ex.StatusCode == HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task GetPresignedUrlAsync_ForDownload_ReturnsUrl()
    {
        // Arrange
        string key = "test/presigned.txt";
        string expectedUrl = $"{TestEndpoint}/{TestBucket}/{key}?signature=xyz";

        _mockS3Client.GetPreSignedURLAsync(
            Arg.Is<GetPreSignedUrlRequest>(r =>
                r.BucketName == TestBucket &&
                r.Key == key &&
                r.Verb == HttpVerb.GET))
            .Returns(expectedUrl);

        // Act
        string url = await _provider.GetPresignedUrlAsync(key, TimeSpan.FromHours(1), forUpload: false);

        // Assert
        url.Should().Be(expectedUrl);
        await _mockS3Client.Received(1).GetPreSignedURLAsync(
            Arg.Is<GetPreSignedUrlRequest>(r => r.Verb == HttpVerb.GET));
    }

    [Fact]
    public async Task GetPresignedUrlAsync_ForUpload_ReturnsUrl()
    {
        // Arrange
        string key = "test/upload-target.txt";
        string expectedUrl = $"{TestEndpoint}/{TestBucket}/{key}?signature=abc";

        _mockS3Client.GetPreSignedURLAsync(
            Arg.Is<GetPreSignedUrlRequest>(r =>
                r.BucketName == TestBucket &&
                r.Key == key &&
                r.Verb == HttpVerb.PUT))
            .Returns(expectedUrl);

        // Act
        string url = await _provider.GetPresignedUrlAsync(key, TimeSpan.FromMinutes(15), forUpload: true);

        // Assert
        url.Should().Be(expectedUrl);
        await _mockS3Client.Received(1).GetPreSignedURLAsync(
            Arg.Is<GetPreSignedUrlRequest>(r => r.Verb == HttpVerb.PUT));
    }

    [Fact]
    public async Task GetPresignedUrlAsync_SetsExpiryCorrectly()
    {
        // Arrange
        string key = "test/expiry.txt";
        TimeSpan expiry = TimeSpan.FromMinutes(30);
        DateTime before = DateTime.UtcNow.Add(expiry).AddSeconds(-5);
        DateTime after = DateTime.UtcNow.Add(expiry).AddSeconds(5);

        _mockS3Client.GetPreSignedURLAsync(
            Arg.Any<GetPreSignedUrlRequest>())
            .Returns("http://test-url");

        // Act
        await _provider.GetPresignedUrlAsync(key, expiry, forUpload: false);

        // Assert
        await _mockS3Client.Received(1).GetPreSignedURLAsync(
            Arg.Is<GetPreSignedUrlRequest>(r =>
                r.Expires >= before && r.Expires <= after));
    }

    public void Dispose()
    {
        (_mockS3Client as IDisposable)?.Dispose();
    }
}
