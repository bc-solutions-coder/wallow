using Wallow.Shared.Contracts.Storage;
using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.Results;
using Wallow.Storage.Application.Commands.CompletePresignedUpload;
using Wallow.Storage.Application.DTOs;
using Wallow.Storage.Application.Interfaces;
using Wallow.Storage.Domain.Entities;
using Wallow.Storage.Domain.Enums;
using Wallow.Storage.Domain.Identity;

namespace Wallow.Storage.Tests.Application.Commands.CompletePresignedUpload;

public class CompletePresignedUploadHandlerTests
{
    private readonly IStoredFileRepository _fileRepository;
    private readonly IStorageProvider _storageProvider;
    private readonly IFileScanner _fileScanner;
    private readonly CompletePresignedUploadHandler _handler;

    public CompletePresignedUploadHandlerTests()
    {
        _fileRepository = Substitute.For<IStoredFileRepository>();
        _storageProvider = Substitute.For<IStorageProvider>();
        _fileScanner = Substitute.For<IFileScanner>();
        _handler = new CompletePresignedUploadHandler(_fileRepository, _storageProvider, _fileScanner);
    }

    [Fact]
    public async Task Handle_WhenFileNotFound_ReturnsNotFound()
    {
        Guid fileId = Guid.NewGuid();
        _fileRepository.GetByIdAsync(Arg.Any<StoredFileId>(), Arg.Any<CancellationToken>())
            .Returns((StoredFile?)null);

        Result<CompletePresignedUploadResult> result = await _handler.Handle(
            new CompletePresignedUploadCommand(fileId), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Contain("NotFound");
    }

    [Fact]
    public async Task Handle_WhenObjectNotUploadedYet_FailsWithoutScanning()
    {
        StoredFile file = CreatePendingFile();
        _fileRepository.GetByIdAsync(Arg.Any<StoredFileId>(), Arg.Any<CancellationToken>())
            .Returns(file);
        _storageProvider.ExistsAsync(file.StorageKey, Arg.Any<CancellationToken>())
            .Returns(false);

        Result<CompletePresignedUploadResult> result = await _handler.Handle(
            new CompletePresignedUploadCommand(file.Id.Value), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Contain("File.NotUploaded");
        file.Status.Should().Be(FileStatus.PendingValidation);
        await _storageProvider.DidNotReceive().DownloadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _fileScanner.DidNotReceive().ScanAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _fileRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenScanIsClean_MarksFileAsAvailable()
    {
        StoredFile file = CreatePendingFile();
        MemoryStream fileStream = new([0x01, 0x02, 0x03]);
        _fileRepository.GetByIdAsync(Arg.Any<StoredFileId>(), Arg.Any<CancellationToken>())
            .Returns(file);
        _storageProvider.ExistsAsync(file.StorageKey, Arg.Any<CancellationToken>())
            .Returns(true);
        _storageProvider.DownloadAsync(file.StorageKey, Arg.Any<CancellationToken>())
            .Returns(fileStream);
        _fileScanner.ScanAsync(fileStream, file.FileName, Arg.Any<CancellationToken>())
            .Returns(FileScanResult.Clean());

        Result<CompletePresignedUploadResult> result = await _handler.Handle(
            new CompletePresignedUploadCommand(file.Id.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(FileStatus.Available);
        file.Status.Should().Be(FileStatus.Available);
        await _fileRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenScanDetectsThreat_MarksFileAsRejected()
    {
        StoredFile file = CreatePendingFile();
        MemoryStream fileStream = new([0x4D, 0x5A, 0x90]);
        _fileRepository.GetByIdAsync(Arg.Any<StoredFileId>(), Arg.Any<CancellationToken>())
            .Returns(file);
        _storageProvider.ExistsAsync(file.StorageKey, Arg.Any<CancellationToken>())
            .Returns(true);
        _storageProvider.DownloadAsync(file.StorageKey, Arg.Any<CancellationToken>())
            .Returns(fileStream);
        _fileScanner.ScanAsync(fileStream, file.FileName, Arg.Any<CancellationToken>())
            .Returns(FileScanResult.Infected("Trojan.GenericKD"));

        Result<CompletePresignedUploadResult> result = await _handler.Handle(
            new CompletePresignedUploadCommand(file.Id.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(FileStatus.Rejected);
        file.Status.Should().Be(FileStatus.Rejected);
        await _fileRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenFileAlreadyAvailable_ReportsStatusWithoutRescanning()
    {
        StoredFile file = CreatePendingFile();
        file.MarkAsAvailable();
        _fileRepository.GetByIdAsync(Arg.Any<StoredFileId>(), Arg.Any<CancellationToken>())
            .Returns(file);

        Result<CompletePresignedUploadResult> result = await _handler.Handle(
            new CompletePresignedUploadCommand(file.Id.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(FileStatus.Available);
        await _storageProvider.DidNotReceive().ExistsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _storageProvider.DidNotReceive().DownloadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _fileScanner.DidNotReceive().ScanAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenFileAlreadyRejected_ReportsStatusWithoutRescanning()
    {
        StoredFile file = CreatePendingFile();
        file.MarkAsRejected();
        _fileRepository.GetByIdAsync(Arg.Any<StoredFileId>(), Arg.Any<CancellationToken>())
            .Returns(file);

        Result<CompletePresignedUploadResult> result = await _handler.Handle(
            new CompletePresignedUploadCommand(file.Id.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(FileStatus.Rejected);
        await _storageProvider.DidNotReceive().DownloadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _fileScanner.DidNotReceive().ScanAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    private static StoredFile CreatePendingFile()
    {
        return StoredFile.CreatePendingValidation(
            TenantId.New(),
            StorageBucketId.New(),
            "report.pdf",
            "application/pdf",
            1000,
            "tenant-x/bucket/report.pdf",
            Guid.NewGuid());
    }
}
