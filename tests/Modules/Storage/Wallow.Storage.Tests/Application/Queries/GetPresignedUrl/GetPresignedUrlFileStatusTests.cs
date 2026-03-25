using Microsoft.Extensions.Options;
using Wallow.Shared.Contracts.Storage;
using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.Results;
using Wallow.Storage.Application.Configuration;
using Wallow.Storage.Application.DTOs;
using Wallow.Storage.Application.Interfaces;
using Wallow.Storage.Application.Queries.GetPresignedUrl;
using Wallow.Storage.Domain.Entities;
using Wallow.Storage.Domain.Identity;

namespace Wallow.Storage.Tests.Application.Queries.GetPresignedUrl;

public class GetPresignedUrlFileStatusTests
{
    private readonly IStoredFileRepository _fileRepository;
    private readonly IStorageProvider _storageProvider;
    private readonly GetPresignedUrlHandler _handler;

    public GetPresignedUrlFileStatusTests()
    {
        _fileRepository = Substitute.For<IStoredFileRepository>();
        _storageProvider = Substitute.For<IStorageProvider>();
        _handler = new GetPresignedUrlHandler(_fileRepository, _storageProvider, Options.Create(new PresignedUrlOptions()));
    }

    [Fact]
    public async Task Handle_WhenFileIsPendingValidation_ReturnsValidationFailure()
    {
        TenantId tenantId = TenantId.New();
        StorageBucketId bucketId = StorageBucketId.New();
        StoredFile file = StoredFile.CreatePendingValidation(
            tenantId, bucketId, "pending.pdf", "application/pdf", 1000, "key", Guid.NewGuid());
        GetPresignedUrlQuery query = new(tenantId.Value, file.Id.Value);

        _fileRepository.GetByIdAsync(Arg.Any<StoredFileId>(), Arg.Any<CancellationToken>())
            .Returns(file);

        Result<PresignedUrlResult> result = await _handler.Handle(query, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Contain("File.NotAvailable");
    }

    [Fact]
    public async Task Handle_WhenFileIsRejected_ReturnsValidationFailure()
    {
        TenantId tenantId = TenantId.New();
        StorageBucketId bucketId = StorageBucketId.New();
        StoredFile file = StoredFile.CreatePendingValidation(
            tenantId, bucketId, "rejected.pdf", "application/pdf", 1000, "key", Guid.NewGuid());
        file.MarkAsRejected();
        GetPresignedUrlQuery query = new(tenantId.Value, file.Id.Value);

        _fileRepository.GetByIdAsync(Arg.Any<StoredFileId>(), Arg.Any<CancellationToken>())
            .Returns(file);

        Result<PresignedUrlResult> result = await _handler.Handle(query, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Contain("File.NotAvailable");
    }

    [Fact]
    public async Task Handle_WhenFileIsAvailable_ReturnsSuccess()
    {
        TenantId tenantId = TenantId.New();
        StorageBucket bucket = StorageBucket.Create(tenantId, "bucket");
        StoredFile file = StoredFile.Create(
            tenantId, bucket.Id, "available.pdf", "application/pdf", 1000, "storage/key", Guid.NewGuid());
        GetPresignedUrlQuery query = new(tenantId.Value, file.Id.Value);

        _fileRepository.GetByIdAsync(Arg.Any<StoredFileId>(), Arg.Any<CancellationToken>())
            .Returns(file);
        _storageProvider.GetPresignedUrlAsync("storage/key", Arg.Any<TimeSpan>(), false, Arg.Any<CancellationToken>())
            .Returns("https://storage.example.com/file");

        Result<PresignedUrlResult> result = await _handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Url.Should().Be("https://storage.example.com/file");
    }

    [Fact]
    public async Task Handle_WhenExpiryExceedsMax_CapsAtMaxExpiry()
    {
        TenantId tenantId = TenantId.New();
        StorageBucket bucket = StorageBucket.Create(tenantId, "bucket");
        StoredFile file = StoredFile.Create(
            tenantId, bucket.Id, "file.pdf", "application/pdf", 1000, "key", Guid.NewGuid());
        // Configure max expiry to 30 minutes, request 2 hours
        PresignedUrlOptions options = new() { MaxDownloadExpiryMinutes = 30 };
        GetPresignedUrlHandler handlerWithMaxExpiry = new(
            _fileRepository, _storageProvider, Options.Create(options));
        TimeSpan expiryOver2Hours = TimeSpan.FromHours(2);
        GetPresignedUrlQuery query = new(tenantId.Value, file.Id.Value, Expiry: expiryOver2Hours);

        _fileRepository.GetByIdAsync(Arg.Any<StoredFileId>(), Arg.Any<CancellationToken>())
            .Returns(file);
        _storageProvider.GetPresignedUrlAsync("key", Arg.Any<TimeSpan>(), false, Arg.Any<CancellationToken>())
            .Returns("https://example.com/url");

        Result<PresignedUrlResult> result = await handlerWithMaxExpiry.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        // Should have capped at 30 minutes
        await _storageProvider.Received(1).GetPresignedUrlAsync(
            "key",
            Arg.Is<TimeSpan>(t => t <= TimeSpan.FromMinutes(30)),
            false,
            Arg.Any<CancellationToken>());
    }
}
