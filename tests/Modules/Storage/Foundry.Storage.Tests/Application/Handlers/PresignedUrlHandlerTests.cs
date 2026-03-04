using Foundry.Shared.Contracts.Storage;
using Foundry.Shared.Kernel.Identity;
using Foundry.Shared.Kernel.Results;
using Foundry.Storage.Application.DTOs;
using Foundry.Storage.Application.Interfaces;
using Foundry.Storage.Application.Queries.GetPresignedUrl;
using Foundry.Storage.Application.Queries.GetUploadPresignedUrl;
using Foundry.Storage.Domain.Entities;
using Foundry.Storage.Domain.Identity;
using Wolverine;

#pragma warning disable CA1861 // Inline arrays in test data initializers

namespace Foundry.Storage.Tests.Application.Handlers;

public class GetPresignedUrlHandlerTests
{
    private readonly IStoredFileRepository _fileRepository;
    private readonly IStorageProvider _storageProvider;
    private readonly GetPresignedUrlHandler _handler;

    public GetPresignedUrlHandlerTests()
    {
        _fileRepository = Substitute.For<IStoredFileRepository>();
        _storageProvider = Substitute.For<IStorageProvider>();
        _handler = new GetPresignedUrlHandler(_fileRepository, _storageProvider);
    }

    [Fact]
    public async Task Handle_WhenFileNotFound_ReturnsNotFoundFailure()
    {
        GetPresignedUrlQuery query = new(Guid.NewGuid(), Guid.NewGuid());

        _fileRepository.GetByIdAsync(Arg.Any<StoredFileId>(), Arg.Any<CancellationToken>())
            .Returns((StoredFile?)null);

        Result<PresignedUrlResult> result = await _handler.Handle(query, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Contain("NotFound");
    }

    [Fact]
    public async Task Handle_WhenFileExistsButWrongTenant_ReturnsNotFoundFailure()
    {
        TenantId fileTenantId = TenantId.New();
        Guid differentTenantId = Guid.NewGuid();
        StorageBucket bucket = StorageBucket.Create(fileTenantId, "bucket");
        StoredFile file = StoredFile.Create(
            fileTenantId, bucket.Id, "test.pdf", "application/pdf", 1000, "key", Guid.NewGuid());
        GetPresignedUrlQuery query = new(differentTenantId, file.Id.Value);

        _fileRepository.GetByIdAsync(Arg.Any<StoredFileId>(), Arg.Any<CancellationToken>())
            .Returns(file);

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
        GetPresignedUrlQuery query = new(tenantId.Value, file.Id.Value);

        _fileRepository.GetByIdAsync(Arg.Any<StoredFileId>(), Arg.Any<CancellationToken>())
            .Returns(file);
        _storageProvider.GetPresignedUrlAsync("storage/key", Arg.Any<TimeSpan>(), false, Arg.Any<CancellationToken>())
            .Returns("https://storage.example.com/presigned-url");

        Result<PresignedUrlResult> result = await _handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Url.Should().Be("https://storage.example.com/presigned-url");
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
        GetPresignedUrlQuery query = new(tenantId.Value, file.Id.Value, Expiry: customExpiry);

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
        GetPresignedUrlQuery query = new(tenantId.Value, file.Id.Value);

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
        GetPresignedUrlQuery query = new(Guid.NewGuid(), Guid.NewGuid());

        _fileRepository.GetByIdAsync(Arg.Any<StoredFileId>(), Arg.Any<CancellationToken>())
            .Returns((StoredFile?)null);

        await _handler.Handle(query, CancellationToken.None);

        await _storageProvider.DidNotReceive().GetPresignedUrlAsync(
            Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_DoesNotCallStorageProvider_WhenTenantMismatch()
    {
        TenantId fileTenantId = TenantId.New();
        StorageBucket bucket = StorageBucket.Create(fileTenantId, "bucket");
        StoredFile file = StoredFile.Create(
            fileTenantId, bucket.Id, "file.txt", "text/plain", 100, "key", Guid.NewGuid());
        GetPresignedUrlQuery query = new(Guid.NewGuid(), file.Id.Value);

        _fileRepository.GetByIdAsync(Arg.Any<StoredFileId>(), Arg.Any<CancellationToken>())
            .Returns(file);

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
    private readonly IMessageBus _messageBus;
    private readonly GetUploadPresignedUrlHandler _handler;

    public GetUploadPresignedUrlHandlerTests()
    {
        _bucketRepository = Substitute.For<IStorageBucketRepository>();
        _fileRepository = Substitute.For<IStoredFileRepository>();
        _storageProvider = Substitute.For<IStorageProvider>();
        _messageBus = Substitute.For<IMessageBus>();
        _handler = new GetUploadPresignedUrlHandler(_bucketRepository, _fileRepository, _storageProvider, _messageBus);
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
            TenantId.New(), "restricted", allowedContentTypes: new[] { "image/png" });
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
}
