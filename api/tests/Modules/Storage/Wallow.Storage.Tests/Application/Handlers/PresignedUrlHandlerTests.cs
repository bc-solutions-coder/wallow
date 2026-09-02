using Microsoft.Extensions.Options;
using Wallow.Shared.Contracts.Storage;
using Wallow.Shared.Kernel.Errors;
using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.Results;
using Wallow.Storage.Application.Configuration;
using Wallow.Storage.Application.DTOs;
using Wallow.Storage.Application.Interfaces;
using Wallow.Storage.Application.Queries.GetPresignedUrl;
using Wallow.Storage.Application.Queries.GetUploadPresignedUrl;
using Wallow.Storage.Application.Settings;
using Wallow.Storage.Domain.Entities;
using Wallow.Storage.Domain.Identity;

#pragma warning disable CA1861 // Inline arrays in test data initializers

namespace Wallow.Storage.Tests.Application.Handlers;

public class GetPresignedUrlHandlerTests
{
    private readonly IStoredFileRepository _fileRepository;
    private readonly IStorageProvider _storageProvider;
    private readonly GetPresignedUrlHandler _handler;

    public GetPresignedUrlHandlerTests()
    {
        _fileRepository = Substitute.For<IStoredFileRepository>();
        _storageProvider = Substitute.For<IStorageProvider>();
        _handler = new GetPresignedUrlHandler(_fileRepository, _storageProvider, Options.Create(new PresignedUrlOptions()));
    }

    [Fact]
    public async Task Handle_WhenFileNotFound_ReturnsNotFoundFailure()
    {
        GetPresignedUrlQuery query = new(Guid.NewGuid());

        _fileRepository.GetByIdAsync(Arg.Any<StoredFileId>(), Arg.Any<CancellationToken>())
            .Returns((StoredFile?)null);

        Result<PresignedUrlResult> result = await _handler.Handle(query, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Contain("NotFound");
    }

    [Fact]
    public async Task Handle_WhenFileExistsAndTenantMatches_ReturnsPresignedUrl()
    {
        TenantId tenantId = TenantId.New();
        StorageBucket bucket = StorageBucket.Create(tenantId, "bucket");
        StoredFile file = StoredFile.Create(
            tenantId, bucket.Id, "report.pdf", "application/pdf", 2000, "storage/key", Guid.NewGuid());
        GetPresignedUrlQuery query = new(file.Id.Value);

        _fileRepository.GetByIdAsync(Arg.Any<StoredFileId>(), Arg.Any<CancellationToken>())
            .Returns(file);
        _storageProvider.GetPresignedUrlAsync("storage/key", Arg.Any<TimeSpan>(), false, Arg.Any<CancellationToken>())
            .Returns("https://storage.example.com/presigned-url");

        Result<PresignedUrlResult> result = await _handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Url.Should().Be("https://storage.example.com/presigned-url");
        result.Value.ExpiresAt.Should().BeAfter(DateTime.UtcNow);
    }

    [Fact]
    public async Task Handle_WithCustomExpiry_UsesProvidedExpiry()
    {
        TenantId tenantId = TenantId.New();
        StorageBucket bucket = StorageBucket.Create(tenantId, "bucket");
        StoredFile file = StoredFile.Create(
            tenantId, bucket.Id, "file.txt", "text/plain", 100, "key", Guid.NewGuid());
        TimeSpan customExpiry = TimeSpan.FromMinutes(30);
        GetPresignedUrlQuery query = new(file.Id.Value, Expiry: customExpiry);

        _fileRepository.GetByIdAsync(Arg.Any<StoredFileId>(), Arg.Any<CancellationToken>())
            .Returns(file);
        _storageProvider.GetPresignedUrlAsync("key", customExpiry, false, Arg.Any<CancellationToken>())
            .Returns("https://example.com/url");

        Result<PresignedUrlResult> result = await _handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _storageProvider.Received(1).GetPresignedUrlAsync("key", customExpiry, false, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithoutExpiry_UsesDefaultOneHourExpiry()
    {
        TenantId tenantId = TenantId.New();
        StorageBucket bucket = StorageBucket.Create(tenantId, "bucket");
        StoredFile file = StoredFile.Create(
            tenantId, bucket.Id, "file.txt", "text/plain", 100, "key", Guid.NewGuid());
        GetPresignedUrlQuery query = new(file.Id.Value);

        _fileRepository.GetByIdAsync(Arg.Any<StoredFileId>(), Arg.Any<CancellationToken>())
            .Returns(file);
        _storageProvider.GetPresignedUrlAsync("key", TimeSpan.FromHours(1), false, Arg.Any<CancellationToken>())
            .Returns("https://example.com/url");

        Result<PresignedUrlResult> result = await _handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _storageProvider.Received(1).GetPresignedUrlAsync("key", TimeSpan.FromHours(1), false, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_DoesNotCallStorageProvider_WhenFileNotFound()
    {
        GetPresignedUrlQuery query = new(Guid.NewGuid());

        _fileRepository.GetByIdAsync(Arg.Any<StoredFileId>(), Arg.Any<CancellationToken>())
            .Returns((StoredFile?)null);

        await _handler.Handle(query, CancellationToken.None);

        await _storageProvider.DidNotReceive().GetPresignedUrlAsync(
            Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }
}

public class GetUploadPresignedUrlHandlerTests
{
    private readonly IStorageBucketRepository _bucketRepository;
    private readonly IStoredFileRepository _fileRepository;
    private readonly IStorageProvider _storageProvider;
    private readonly IStorageLimitsProvider _limitsProvider;
    private readonly GetUploadPresignedUrlHandler _handler;

    public GetUploadPresignedUrlHandlerTests()
    {
        _bucketRepository = Substitute.For<IStorageBucketRepository>();
        _fileRepository = Substitute.For<IStoredFileRepository>();
        _storageProvider = Substitute.For<IStorageProvider>();
        _limitsProvider = Substitute.For<IStorageLimitsProvider>();
        _limitsProvider.GetLimitsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(StorageLimits.Create(50, "*", 1024));
        _handler = new GetUploadPresignedUrlHandler(_bucketRepository, _fileRepository, _storageProvider, Options.Create(new PresignedUrlOptions()), _limitsProvider);
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
            TenantId.New(), "images-only", allowedContentTypes: ["image/*"]);
        GetUploadPresignedUrlQuery query = new(
            Guid.NewGuid(), Guid.NewGuid(), "images-only", "doc.pdf", "application/pdf", 100);

        _bucketRepository.GetByNameAsync("images-only", Arg.Any<CancellationToken>())
            .Returns(bucket);

        Result<PresignedUploadResult> result = await _handler.Handle(query, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Kind.Should().Be(ErrorKind.Validation);
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
        result.Error.Kind.Should().Be(ErrorKind.Validation);
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
        result.Value.UploadUrl.Should().Be("https://storage.example.com/upload-url");
        result.Value.ExpiresAt.Should().BeAfter(DateTime.UtcNow);
    }

    [Fact]
    public async Task Handle_WithCustomExpiry_UsesProvidedExpiry()
    {
        Guid tenantId = Guid.NewGuid();
        StorageBucket bucket = StorageBucket.Create(TenantId.New(), "bucket");
        TimeSpan customExpiry = TimeSpan.FromMinutes(10); // Within 15-minute max
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

    [Fact]
    public async Task Handle_DoesNotCallStorageProvider_WhenBucketNotFound()
    {
        GetUploadPresignedUrlQuery query = new(Guid.NewGuid(), Guid.NewGuid(), "missing", "file.txt", "text/plain", 100);

        _bucketRepository.GetByNameAsync("missing", Arg.Any<CancellationToken>())
            .Returns((StorageBucket?)null);

        await _handler.Handle(query, CancellationToken.None);

        await _storageProvider.DidNotReceive().GetPresignedUrlAsync(
            Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_DoesNotCallStorageProvider_WhenContentTypeNotAllowed()
    {
        StorageBucket bucket = StorageBucket.Create(
            TenantId.New(), "restricted", allowedContentTypes: ["image/png"]);
        GetUploadPresignedUrlQuery query = new(
            Guid.NewGuid(), Guid.NewGuid(), "restricted", "file.exe", "application/octet-stream", 100);

        _bucketRepository.GetByNameAsync("restricted", Arg.Any<CancellationToken>())
            .Returns(bucket);

        await _handler.Handle(query, CancellationToken.None);

        await _storageProvider.DidNotReceive().GetPresignedUrlAsync(
            Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_DoesNotCallStorageProvider_WhenFileSizeExceeded()
    {
        StorageBucket bucket = StorageBucket.Create(
            TenantId.New(), "limited", maxFileSizeBytes: 500);
        GetUploadPresignedUrlQuery query = new(
            Guid.NewGuid(), Guid.NewGuid(), "limited", "big.bin", "application/octet-stream", 1000);

        _bucketRepository.GetByNameAsync("limited", Arg.Any<CancellationToken>())
            .Returns(bucket);

        await _handler.Handle(query, CancellationToken.None);

        await _storageProvider.DidNotReceive().GetPresignedUrlAsync(
            Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenSizeExceedsTenantUploadLimit_ReturnsValidationFailure()
    {
        StorageBucket bucket = StorageBucket.Create(TenantId.New(), "uploads");
        GetUploadPresignedUrlQuery query = new(
            Guid.NewGuid(), Guid.NewGuid(), "uploads", "big.png", "image/png", 2L * 1024 * 1024);

        _bucketRepository.GetByNameAsync("uploads", Arg.Any<CancellationToken>())
            .Returns(bucket);
        _limitsProvider.GetLimitsAsync(query.TenantId, Arg.Any<CancellationToken>())
            .Returns(StorageLimits.Create(1, "*", 1024));

        Result<PresignedUploadResult> result = await _handler.Handle(query, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Kind.Should().Be(ErrorKind.Validation);
        result.Error.Message.Should().Contain("upload limit");
        _fileRepository.DidNotReceive().Add(Arg.Any<StoredFile>());
    }

    [Fact]
    public async Task Handle_WhenExtensionNotInTenantAllowlist_ReturnsValidationFailure()
    {
        StorageBucket bucket = StorageBucket.Create(TenantId.New(), "uploads");
        GetUploadPresignedUrlQuery query = new(
            Guid.NewGuid(), Guid.NewGuid(), "uploads", "script.exe", "application/octet-stream", 100);

        _bucketRepository.GetByNameAsync("uploads", Arg.Any<CancellationToken>())
            .Returns(bucket);
        _limitsProvider.GetLimitsAsync(query.TenantId, Arg.Any<CancellationToken>())
            .Returns(StorageLimits.Create(50, "jpg,png", 1024));

        Result<PresignedUploadResult> result = await _handler.Handle(query, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Kind.Should().Be(ErrorKind.Validation);
        result.Error.Message.Should().Contain("not allowed");
        _fileRepository.DidNotReceive().Add(Arg.Any<StoredFile>());
    }

    [Fact]
    public async Task Handle_WhenTenantQuotaWouldBeExceeded_ReturnsValidationFailure()
    {
        StorageBucket bucket = StorageBucket.Create(TenantId.New(), "uploads");
        GetUploadPresignedUrlQuery query = new(
            Guid.NewGuid(), Guid.NewGuid(), "uploads", "photo.png", "image/png", 1000);

        _bucketRepository.GetByNameAsync("uploads", Arg.Any<CancellationToken>())
            .Returns(bucket);
        _limitsProvider.GetLimitsAsync(query.TenantId, Arg.Any<CancellationToken>())
            .Returns(StorageLimits.Create(50, "*", 1));
        _fileRepository.GetTotalSizeBytesAsync(Arg.Any<CancellationToken>())
            .Returns((1024L * 1024) - 500);

        Result<PresignedUploadResult> result = await _handler.Handle(query, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Kind.Should().Be(ErrorKind.Validation);
        result.Error.Message.Should().Contain("quota");
        _fileRepository.DidNotReceive().Add(Arg.Any<StoredFile>());
    }
}
