using Foundry.Shared.Contracts.Storage;
using Foundry.Shared.Kernel.Identity;
using Foundry.Shared.Kernel.Results;
using Foundry.Storage.Application.Configuration;
using Foundry.Storage.Application.DTOs;
using Foundry.Storage.Application.Interfaces;
using Foundry.Storage.Application.Queries.GetPresignedUrl;
using Foundry.Storage.Domain.Entities;
using Foundry.Storage.Domain.Identity;
using Microsoft.Extensions.Options;

namespace Foundry.Storage.Tests.Application;

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
}
