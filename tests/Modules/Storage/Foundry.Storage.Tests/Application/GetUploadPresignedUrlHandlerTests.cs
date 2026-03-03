using Foundry.Shared.Contracts.Storage;
using Foundry.Shared.Kernel.Identity;
using Foundry.Shared.Kernel.Results;
using Foundry.Storage.Application.DTOs;
using Foundry.Storage.Application.Interfaces;
using Foundry.Storage.Application.Queries.GetUploadPresignedUrl;
using Foundry.Storage.Domain.Entities;

#pragma warning disable CA1861 // Inline arrays in test data initializers

namespace Foundry.Storage.Tests.Application;

public class GetUploadPresignedUrlHandlerTests
{
    private readonly IStorageBucketRepository _bucketRepository;
    private readonly IStoredFileRepository _fileRepository;
    private readonly IStorageProvider _storageProvider;
    private readonly GetUploadPresignedUrlHandler _handler;

    public GetUploadPresignedUrlHandlerTests()
    {
        _bucketRepository = Substitute.For<IStorageBucketRepository>();
        _fileRepository = Substitute.For<IStoredFileRepository>();
        _storageProvider = Substitute.For<IStorageProvider>();
        _handler = new GetUploadPresignedUrlHandler(_bucketRepository, _fileRepository, _storageProvider);
    }

    [Fact]
    public async Task Handle_WhenBucketNotFound_ReturnsNotFoundFailure()
    {
        GetUploadPresignedUrlQuery query = new(Guid.NewGuid(), Guid.NewGuid(), "nonexistent", "file.txt", "text/plain", 100);

        _bucketRepository.GetByNameAsync("nonexistent", Arg.Any<CancellationToken>())
            .Returns((StorageBucket?)null);

        Result<PresignedUploadResult> result = await _handler.Handle(query, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Contain("NotFound");
    }

    [Fact]
    public async Task Handle_WhenContentTypeNotAllowed_ReturnsValidationFailure()
    {
        StorageBucket bucket = StorageBucket.Create(
            TenantId.New(), "images-only", allowedContentTypes: new[] { "image/*" });
        GetUploadPresignedUrlQuery query = new(
            Guid.NewGuid(), Guid.NewGuid(), "images-only", "doc.pdf", "application/pdf", 100);

        _bucketRepository.GetByNameAsync("images-only", Arg.Any<CancellationToken>())
            .Returns(bucket);

        Result<PresignedUploadResult> result = await _handler.Handle(query, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().StartWith("Validation");
        result.Error.Message.Should().Contain("Content type");
    }

    [Fact]
    public async Task Handle_WhenFileSizeExceeded_ReturnsValidationFailure()
    {
        StorageBucket bucket = StorageBucket.Create(
            TenantId.New(), "small-bucket", maxFileSizeBytes: 1000);
        GetUploadPresignedUrlQuery query = new(
            Guid.NewGuid(), Guid.NewGuid(), "small-bucket", "big.zip", "application/zip", 5000);

        _bucketRepository.GetByNameAsync("small-bucket", Arg.Any<CancellationToken>())
            .Returns(bucket);

        Result<PresignedUploadResult> result = await _handler.Handle(query, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().StartWith("Validation");
        result.Error.Message.Should().Contain("size");
    }

    [Fact]
    public async Task Handle_WhenValid_ReturnsPresignedUploadUrl()
    {
        Guid tenantId = Guid.NewGuid();
        StorageBucket bucket = StorageBucket.Create(TenantId.New(), "uploads");
        GetUploadPresignedUrlQuery query = new(
            tenantId, Guid.NewGuid(), "uploads", "photo.png", "image/png", 2048);

        _bucketRepository.GetByNameAsync("uploads", Arg.Any<CancellationToken>())
            .Returns(bucket);
        _storageProvider.GetPresignedUrlAsync(
                Arg.Any<string>(), Arg.Any<TimeSpan>(), true, Arg.Any<CancellationToken>())
            .Returns("https://storage.example.com/upload-url");

        Result<PresignedUploadResult> result = await _handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.UploadUrl.Should().Be("https://storage.example.com/upload-url");
        result.Value.StorageKey.Should().Contain($"tenant-{tenantId}");
        result.Value.StorageKey.Should().Contain("uploads");
        result.Value.ExpiresAt.Should().BeAfter(DateTime.UtcNow);
    }

    [Fact]
    public async Task Handle_WithCustomExpiry_UsesProvidedExpiry()
    {
        Guid tenantId = Guid.NewGuid();
        StorageBucket bucket = StorageBucket.Create(TenantId.New(), "bucket");
        TimeSpan customExpiry = TimeSpan.FromMinutes(60);
        GetUploadPresignedUrlQuery query = new(
            tenantId, Guid.NewGuid(), "bucket", "file.txt", "text/plain", 100, Expiry: customExpiry);

        _bucketRepository.GetByNameAsync("bucket", Arg.Any<CancellationToken>())
            .Returns(bucket);
        _storageProvider.GetPresignedUrlAsync(
                Arg.Any<string>(), customExpiry, true, Arg.Any<CancellationToken>())
            .Returns("https://example.com/url");

        Result<PresignedUploadResult> result = await _handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _storageProvider.Received(1).GetPresignedUrlAsync(
            Arg.Any<string>(), customExpiry, true, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithPath_IncludesPathInStorageKey()
    {
        Guid tenantId = Guid.NewGuid();
        StorageBucket bucket = StorageBucket.Create(TenantId.New(), "docs");
        GetUploadPresignedUrlQuery query = new(
            tenantId, Guid.NewGuid(), "docs", "report.pdf", "application/pdf", 500, Path: "reports/2024");

        _bucketRepository.GetByNameAsync("docs", Arg.Any<CancellationToken>())
            .Returns(bucket);
        _storageProvider.GetPresignedUrlAsync(
                Arg.Any<string>(), Arg.Any<TimeSpan>(), true, Arg.Any<CancellationToken>())
            .Returns("https://example.com/url");

        Result<PresignedUploadResult> result = await _handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.StorageKey.Should().Contain("reports/2024");
    }

    [Fact]
    public async Task Handle_WithoutExpiry_UsesDefault15MinuteExpiry()
    {
        StorageBucket bucket = StorageBucket.Create(TenantId.New(), "bucket");
        GetUploadPresignedUrlQuery query = new(
            Guid.NewGuid(), Guid.NewGuid(), "bucket", "file.txt", "text/plain", 100);

        _bucketRepository.GetByNameAsync("bucket", Arg.Any<CancellationToken>())
            .Returns(bucket);
        _storageProvider.GetPresignedUrlAsync(
                Arg.Any<string>(), TimeSpan.FromMinutes(15), true, Arg.Any<CancellationToken>())
            .Returns("https://example.com/url");

        Result<PresignedUploadResult> result = await _handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _storageProvider.Received(1).GetPresignedUrlAsync(
            Arg.Any<string>(), TimeSpan.FromMinutes(15), true, Arg.Any<CancellationToken>());
    }
}
