using Wallow.Shared.Contracts.Storage;
using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.Results;
using Wallow.Storage.Application.Commands.DeleteFile;
using Wallow.Storage.Application.Interfaces;
using Wallow.Storage.Domain.Entities;

namespace Wallow.Storage.Tests.Application;

public class DeleteFileHandlerTests
{
    private readonly IStoredFileRepository _fileRepository;
    private readonly IStorageProvider _storageProvider;
    private readonly DeleteFileHandler _handler;

    public DeleteFileHandlerTests()
    {
        _fileRepository = Substitute.For<IStoredFileRepository>();
        _storageProvider = Substitute.For<IStorageProvider>();
        _handler = new DeleteFileHandler(_fileRepository, _storageProvider);
    }

    [Fact]
    public async Task Handle_WhenFileNotFound_ReturnsNotFoundFailure()
    {
        Guid fileId = Guid.NewGuid();
        DeleteFileCommand command = new(fileId);

        _fileRepository.GetByIdAsync(Arg.Any<Storage.Domain.Identity.StoredFileId>(), Arg.Any<CancellationToken>())
            .Returns((StoredFile?)null);

        Result result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Contain("NotFound");
    }

    [Fact]
    public async Task Handle_WhenFileExistsAndCorrectTenant_DeletesFileAndStorage()
    {
        TenantId tenantId = TenantId.New();
        StorageBucket bucket = StorageBucket.Create(tenantId, "bucket");
        StoredFile file = StoredFile.Create(
            tenantId, bucket.Id, "test.txt", "text/plain", 100, "storage/key/file.txt", Guid.NewGuid());
        DeleteFileCommand command = new(file.Id.Value);

        _fileRepository.GetByIdAsync(Arg.Any<Storage.Domain.Identity.StoredFileId>(), Arg.Any<CancellationToken>())
            .Returns(file);

        Result result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _storageProvider.Received(1).DeleteAsync("storage/key/file.txt", Arg.Any<CancellationToken>());
        _fileRepository.Received(1).Remove(file);
        await _fileRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_CommitsRowRemovalBeforeDeletingFromStorage()
    {
        TenantId tenantId = TenantId.New();
        StorageBucket bucket = StorageBucket.Create(tenantId, "bucket");
        StoredFile file = StoredFile.Create(
            tenantId, bucket.Id, "test.txt", "text/plain", 100, "storage/key/file.txt", Guid.NewGuid());
        DeleteFileCommand command = new(file.Id.Value);

        _fileRepository.GetByIdAsync(Arg.Any<Storage.Domain.Identity.StoredFileId>(), Arg.Any<CancellationToken>())
            .Returns(file);

        await _handler.Handle(command, CancellationToken.None);

        // An object-store delete is not undone by a database rollback, so deleting first and
        // committing second lets a failed commit strand a row pointing at bytes that are already
        // gone -- a permanent 404 on read. Committing first makes the worst case an orphaned
        // object instead: garbage, but nothing points at it.
        Received.InOrder(() =>
        {
            _fileRepository.Remove(file);
            _fileRepository.SaveChangesAsync(Arg.Any<CancellationToken>());
            _storageProvider.DeleteAsync("storage/key/file.txt", Arg.Any<CancellationToken>());
        });
    }
}
