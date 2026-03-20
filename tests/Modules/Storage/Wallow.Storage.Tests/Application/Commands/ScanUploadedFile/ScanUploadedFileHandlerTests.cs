using Wallow.Shared.Contracts.Storage;
using Wallow.Shared.Kernel.Identity;
using Wallow.Storage.Application.Commands.ScanUploadedFile;
using Wallow.Storage.Application.Interfaces;
using Wallow.Storage.Domain.Entities;
using Wallow.Storage.Domain.Enums;
using Wallow.Storage.Domain.Identity;
using Microsoft.Extensions.Logging;

namespace Wallow.Storage.Tests.Application.Commands.ScanUploadedFile;

public class ScanUploadedFileHandlerTests
{
    private readonly IStoredFileRepository _fileRepository;
    private readonly IStorageProvider _storageProvider;
    private readonly IFileScanner _fileScanner;
    private readonly ILogger<ScanUploadedFileCommand> _logger;

    public ScanUploadedFileHandlerTests()
    {
        _fileRepository = Substitute.For<IStoredFileRepository>();
        _storageProvider = Substitute.For<IStorageProvider>();
        _fileScanner = Substitute.For<IFileScanner>();
        _logger = Substitute.For<ILogger<ScanUploadedFileCommand>>();
    }

    [Fact]
    public async Task HandleAsync_WhenFileNotFound_ReturnsEarlyWithoutScanning()
    {
        StoredFileId fileId = StoredFileId.New();
        ScanUploadedFileCommand command = new(fileId);

        _fileRepository.GetByIdAsync(fileId, Arg.Any<CancellationToken>())
            .Returns((StoredFile?)null);

        await ScanUploadedFileHandler.HandleAsync(
            command, _fileRepository, _storageProvider, _fileScanner, _logger, CancellationToken.None);

        await _storageProvider.DidNotReceive().DownloadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _fileScanner.DidNotReceive().ScanAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _fileRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WhenScanIsClean_MarksFileAsAvailable()
    {
        StoredFile storedFile = CreatePendingFile();
        ScanUploadedFileCommand command = new(storedFile.Id);
        MemoryStream fileStream = new([0x01, 0x02, 0x03]);

        _fileRepository.GetByIdAsync(storedFile.Id, Arg.Any<CancellationToken>())
            .Returns(storedFile);
        _storageProvider.DownloadAsync(storedFile.StorageKey, Arg.Any<CancellationToken>())
            .Returns(fileStream);
        _fileScanner.ScanAsync(fileStream, storedFile.FileName, Arg.Any<CancellationToken>())
            .Returns(FileScanResult.Clean());

        await ScanUploadedFileHandler.HandleAsync(
            command, _fileRepository, _storageProvider, _fileScanner, _logger, CancellationToken.None);

        storedFile.Status.Should().Be(FileStatus.Available);
        await _fileRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WhenScanDetectsThreat_MarksFileAsRejected()
    {
        StoredFile storedFile = CreatePendingFile();
        ScanUploadedFileCommand command = new(storedFile.Id);
        MemoryStream fileStream = new([0x4D, 0x5A, 0x90]);

        _fileRepository.GetByIdAsync(storedFile.Id, Arg.Any<CancellationToken>())
            .Returns(storedFile);
        _storageProvider.DownloadAsync(storedFile.StorageKey, Arg.Any<CancellationToken>())
            .Returns(fileStream);
        _fileScanner.ScanAsync(fileStream, storedFile.FileName, Arg.Any<CancellationToken>())
            .Returns(FileScanResult.Infected("Trojan.GenericKD"));

        await ScanUploadedFileHandler.HandleAsync(
            command, _fileRepository, _storageProvider, _fileScanner, _logger, CancellationToken.None);

        storedFile.Status.Should().Be(FileStatus.Rejected);
        await _fileRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WhenScanIsClean_DownloadsFileByStorageKey()
    {
        StoredFile storedFile = CreatePendingFile();
        ScanUploadedFileCommand command = new(storedFile.Id);
        MemoryStream fileStream = new([0x01]);

        _fileRepository.GetByIdAsync(storedFile.Id, Arg.Any<CancellationToken>())
            .Returns(storedFile);
        _storageProvider.DownloadAsync(storedFile.StorageKey, Arg.Any<CancellationToken>())
            .Returns(fileStream);
        _fileScanner.ScanAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(FileScanResult.Clean());

        await ScanUploadedFileHandler.HandleAsync(
            command, _fileRepository, _storageProvider, _fileScanner, _logger, CancellationToken.None);

        await _storageProvider.Received(1).DownloadAsync(storedFile.StorageKey, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WhenScanDetectsThreatWithNullName_MarksFileAsRejected()
    {
        StoredFile storedFile = CreatePendingFile();
        ScanUploadedFileCommand command = new(storedFile.Id);
        MemoryStream fileStream = new([0x01]);

        _fileRepository.GetByIdAsync(storedFile.Id, Arg.Any<CancellationToken>())
            .Returns(storedFile);
        _storageProvider.DownloadAsync(storedFile.StorageKey, Arg.Any<CancellationToken>())
            .Returns(fileStream);
        _fileScanner.ScanAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new FileScanResult(false, null));

        await ScanUploadedFileHandler.HandleAsync(
            command, _fileRepository, _storageProvider, _fileScanner, _logger, CancellationToken.None);

        storedFile.Status.Should().Be(FileStatus.Rejected);
    }

    private static StoredFile CreatePendingFile()
    {
        TenantId tenantId = TenantId.New();
        StorageBucketId bucketId = StorageBucketId.New();
        return StoredFile.CreatePendingValidation(
            tenantId,
            bucketId,
            "test-file.pdf",
            "application/pdf",
            5000,
            $"tenant-{tenantId.Value}/uploads/test-file.pdf",
            Guid.NewGuid());
    }
}
