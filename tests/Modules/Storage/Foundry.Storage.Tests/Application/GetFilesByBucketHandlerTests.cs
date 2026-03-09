using Foundry.Shared.Kernel.Identity;
using Foundry.Shared.Kernel.Pagination;
using Foundry.Shared.Kernel.Results;
using Foundry.Storage.Application.DTOs;
using Foundry.Storage.Application.Interfaces;
using Foundry.Storage.Application.Queries.GetFilesByBucket;
using Foundry.Storage.Domain.Entities;

namespace Foundry.Storage.Tests.Application;

public class GetFilesByBucketHandlerTests
{
    private readonly IStorageBucketRepository _bucketRepository;
    private readonly IStoredFileRepository _fileRepository;
    private readonly GetFilesByBucketHandler _handler;

    public GetFilesByBucketHandlerTests()
    {
        _bucketRepository = Substitute.For<IStorageBucketRepository>();
        _fileRepository = Substitute.For<IStoredFileRepository>();
        _handler = new GetFilesByBucketHandler(_bucketRepository, _fileRepository);
    }

    [Fact]
    public async Task Handle_WhenBucketNotFound_ReturnsNotFoundFailure()
    {
        GetFilesByBucketQuery query = new(Guid.NewGuid(), "nonexistent");

        _bucketRepository.GetByNameAsync("nonexistent", Arg.Any<CancellationToken>())
            .Returns((StorageBucket?)null);

        Result<PagedResult<StoredFileDto>> result = await _handler.Handle(query, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Contain("NotFound");
    }

    [Fact]
    public async Task Handle_WhenBucketExistsWithFiles_ReturnsPagedFiles()
    {
        TenantId tenantId = TenantId.New();
        StorageBucket bucket = StorageBucket.Create(tenantId, "shared-bucket");

        StoredFile tenantFile = StoredFile.Create(
            tenantId, bucket.Id, "mine.txt", "text/plain", 100, "key1", Guid.NewGuid());

        PagedResult<StoredFile> pagedFiles = new(
            new List<StoredFile> { tenantFile }, 1, 1, 20);

        GetFilesByBucketQuery query = new(tenantId.Value, "shared-bucket");

        _bucketRepository.GetByNameAsync("shared-bucket", Arg.Any<CancellationToken>())
            .Returns(bucket);
        _fileRepository.GetByBucketIdPagedAsync(bucket.Id, tenantId.Value, null, 1, 20, Arg.Any<CancellationToken>())
            .Returns(pagedFiles);

        Result<PagedResult<StoredFileDto>> result = await _handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(1);
        result.Value.Items[0].FileName.Should().Be("mine.txt");
        result.Value.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WhenBucketExistsButEmpty_ReturnsEmptyPagedResult()
    {
        TenantId tenantId = TenantId.New();
        StorageBucket bucket = StorageBucket.Create(tenantId, "empty-bucket");
        GetFilesByBucketQuery query = new(tenantId.Value, "empty-bucket");

        PagedResult<StoredFile> emptyPaged = new(
            new List<StoredFile>(), 0, 1, 20);

        _bucketRepository.GetByNameAsync("empty-bucket", Arg.Any<CancellationToken>())
            .Returns(bucket);
        _fileRepository.GetByBucketIdPagedAsync(bucket.Id, tenantId.Value, null, 1, 20, Arg.Any<CancellationToken>())
            .Returns(emptyPaged);

        Result<PagedResult<StoredFileDto>> result = await _handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().BeEmpty();
        result.Value.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WithPathPrefix_PassesPrefixToRepository()
    {
        TenantId tenantId = TenantId.New();
        StorageBucket bucket = StorageBucket.Create(tenantId, "bucket");
        GetFilesByBucketQuery query = new(tenantId.Value, "bucket", PathPrefix: "documents/2024");

        PagedResult<StoredFile> emptyPaged = new(
            new List<StoredFile>(), 0, 1, 20);

        _bucketRepository.GetByNameAsync("bucket", Arg.Any<CancellationToken>())
            .Returns(bucket);
        _fileRepository.GetByBucketIdPagedAsync(bucket.Id, tenantId.Value, "documents/2024", 1, 20, Arg.Any<CancellationToken>())
            .Returns(emptyPaged);

        Result<PagedResult<StoredFileDto>> result = await _handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _fileRepository.Received(1).GetByBucketIdPagedAsync(bucket.Id, tenantId.Value, "documents/2024", 1, 20, Arg.Any<CancellationToken>());
    }
}
