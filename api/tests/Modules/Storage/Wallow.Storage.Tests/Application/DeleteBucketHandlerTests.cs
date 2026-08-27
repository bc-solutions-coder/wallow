using Wallow.Shared.Contracts.Storage;
using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.Results;
using Wallow.Storage.Application.Commands.DeleteBucket;
using Wallow.Storage.Application.Interfaces;
using Wallow.Storage.Domain.Entities;

namespace Wallow.Storage.Tests.Application;

public class DeleteBucketHandlerTests
{
    private readonly IStorageBucketRepository _bucketRepository;
    private readonly IStoredFileRepository _fileRepository;
    private readonly IStorageProvider _storageProvider;
    private readonly DeleteBucketHandler _handler;

    public DeleteBucketHandlerTests()
    {
        _bucketRepository = Substitute.For<IStorageBucketRepository>();
        _fileRepository = Substitute.For<IStoredFileRepository>();
        _storageProvider = Substitute.For<IStorageProvider>();
        _handler = new DeleteBucketHandler(_bucketRepository, _fileRepository, _storageProvider);
    }

    [Fact]
    public async Task Handle_WhenBucketNotFound_ReturnsNotFoundFailure()
    {
        DeleteBucketCommand command = new(Guid.NewGuid(), "nonexistent");
        _bucketRepository.GetByNameAsync(command.Name, Arg.Any<CancellationToken>())
            .Returns((StorageBucket?)null);

        Result result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Contain("NotFound");
    }

    [Fact]
    public async Task Handle_WhenTenantIdDoesNotMatch_ReturnsNotFoundFailure()
    {
        TenantId bucketTenantId = TenantId.New();
        Guid differentTenantId = Guid.NewGuid();
        StorageBucket bucket = StorageBucket.Create(bucketTenantId, "tenant-mismatch");
        DeleteBucketCommand command = new(differentTenantId, "tenant-mismatch");

        _bucketRepository.GetByNameAsync(command.Name, Arg.Any<CancellationToken>())
            .Returns(bucket);

        Result result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Contain("NotFound");
    }

    [Fact]
    public async Task Handle_WhenBucketHasFilesAndNotForced_ReturnsValidationFailure()
    {
        TenantId tenantId = TenantId.New();
        StorageBucket bucket = StorageBucket.Create(tenantId, "has-files");
        StoredFile file = StoredFile.Create(
            tenantId, bucket.Id, "test.txt", "text/plain", 100, "key", Guid.NewGuid());
        DeleteBucketCommand command = new(tenantId.Value, "has-files", Force: false);

        _bucketRepository.GetByNameAsync(command.Name, Arg.Any<CancellationToken>())
            .Returns(bucket);
        _fileRepository.GetByBucketIdAsync(bucket.Id, cancellationToken: Arg.Any<CancellationToken>())
            .Returns(new List<StoredFile> { file });

        Result result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().StartWith("Validation");
        result.Error.Message.Should().Contain("1 file(s)");
        _bucketRepository.DidNotReceive().Remove(Arg.Any<StorageBucket>());
    }

    [Fact]
    public async Task Handle_WhenBucketHasFilesAndForced_DeletesFilesAndBucket()
    {
        TenantId tenantId = TenantId.New();
        StorageBucket bucket = StorageBucket.Create(tenantId, "force-delete");
        StoredFile file1 = StoredFile.Create(
            tenantId, bucket.Id, "file1.txt", "text/plain", 100, "key1", Guid.NewGuid());
        StoredFile file2 = StoredFile.Create(
            tenantId, bucket.Id, "file2.txt", "text/plain", 200, "key2", Guid.NewGuid());
        DeleteBucketCommand command = new(tenantId.Value, "force-delete", Force: true);

        _bucketRepository.GetByNameAsync(command.Name, Arg.Any<CancellationToken>())
            .Returns(bucket);
        _fileRepository.GetByBucketIdAsync(bucket.Id, cancellationToken: Arg.Any<CancellationToken>())
            .Returns(new List<StoredFile> { file1, file2 });

        Result result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _storageProvider.Received(1).DeleteAsync("key1", Arg.Any<CancellationToken>());
        await _storageProvider.Received(1).DeleteAsync("key2", Arg.Any<CancellationToken>());
        _fileRepository.Received(1).Remove(file1);
        _fileRepository.Received(1).Remove(file2);
        _bucketRepository.Received(1).Remove(bucket);
        await _bucketRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenForced_CommitsRemovalsBeforeDeletingFromStorage()
    {
        TenantId tenantId = TenantId.New();
        StorageBucket bucket = StorageBucket.Create(tenantId, "force-delete-ordering");
        StoredFile file1 = StoredFile.Create(
            tenantId, bucket.Id, "file1.txt", "text/plain", 100, "key1", Guid.NewGuid());
        StoredFile file2 = StoredFile.Create(
            tenantId, bucket.Id, "file2.txt", "text/plain", 200, "key2", Guid.NewGuid());
        DeleteBucketCommand command = new(tenantId.Value, "force-delete-ordering", Force: true);

        _bucketRepository.GetByNameAsync(command.Name, Arg.Any<CancellationToken>())
            .Returns(bucket);
        _fileRepository.GetByBucketIdAsync(bucket.Id, cancellationToken: Arg.Any<CancellationToken>())
            .Returns(new List<StoredFile> { file1, file2 });

        await _handler.Handle(command, CancellationToken.None);

        // Same ordering rule as DeleteFileHandler, and sharper here because the loop can strand a
        // whole bucket's worth of rows: object-store deletes survive a database rollback, so they
        // run only once the removals have committed.
        Received.InOrder(() =>
        {
            _fileRepository.Remove(file1);
            _fileRepository.Remove(file2);
            _bucketRepository.Remove(bucket);
            _bucketRepository.SaveChangesAsync(Arg.Any<CancellationToken>());
            _storageProvider.DeleteAsync("key1", Arg.Any<CancellationToken>());
            _storageProvider.DeleteAsync("key2", Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task Handle_WhenBucketIsEmpty_DeletesBucketWithoutForce()
    {
        TenantId tenantId = TenantId.New();
        StorageBucket bucket = StorageBucket.Create(tenantId, "empty-bucket");
        DeleteBucketCommand command = new(tenantId.Value, "empty-bucket");

        _bucketRepository.GetByNameAsync(command.Name, Arg.Any<CancellationToken>())
            .Returns(bucket);
        _fileRepository.GetByBucketIdAsync(bucket.Id, cancellationToken: Arg.Any<CancellationToken>())
            .Returns(new List<StoredFile>());

        Result result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _bucketRepository.Received(1).Remove(bucket);
        await _bucketRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _storageProvider.DidNotReceive().DeleteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenBucketIsEmptyAndForced_DeletesBucket()
    {
        TenantId tenantId = TenantId.New();
        StorageBucket bucket = StorageBucket.Create(tenantId, "empty-forced");
        DeleteBucketCommand command = new(tenantId.Value, "empty-forced", Force: true);

        _bucketRepository.GetByNameAsync(command.Name, Arg.Any<CancellationToken>())
            .Returns(bucket);
        _fileRepository.GetByBucketIdAsync(bucket.Id, cancellationToken: Arg.Any<CancellationToken>())
            .Returns(new List<StoredFile>());

        Result result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _bucketRepository.Received(1).Remove(bucket);
    }
}
