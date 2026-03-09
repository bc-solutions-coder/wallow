using Foundry.Shared.Contracts.Storage;
using Foundry.Shared.Contracts.Storage.Commands;
using Foundry.Shared.Kernel.Identity;
using Foundry.Shared.Kernel.Results;
using Foundry.Storage.Application.Commands.UploadFile;
using Foundry.Storage.Application.Interfaces;
using Foundry.Storage.Domain.Entities;

#pragma warning disable CA1861 // Inline arrays in test data initializers

namespace Foundry.Storage.Tests.Application;

public class UploadFileHandlerTests
{
    private readonly IStorageBucketRepository _bucketRepository;
    private readonly IStoredFileRepository _fileRepository;
    private readonly IStorageProvider _storageProvider;
    private readonly IFileScanner _fileScanner;
    private readonly UploadFileHandler _handler;

    public UploadFileHandlerTests()
    {
        _bucketRepository = Substitute.For<IStorageBucketRepository>();
        _fileRepository = Substitute.For<IStoredFileRepository>();
        _storageProvider = Substitute.For<IStorageProvider>();
        _fileScanner = Substitute.For<IFileScanner>();
        _fileScanner.ScanAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(FileScanResult.Clean());
        _handler = new UploadFileHandler(_bucketRepository, _fileRepository, _storageProvider, _fileScanner);
    }

    [Fact]
    public async Task Handle_WhenBucketNotFound_ReturnsFailure()
    {
        // Arrange
        UploadFileCommand command = CreateCommand();
        _bucketRepository.GetByNameAsync(command.BucketName, Arg.Any<CancellationToken>())
            .Returns((StorageBucket?)null);

        // Act
        Result<UploadResult> result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Contain("NotFound");
    }

    [Fact]
    public async Task Handle_WhenContentTypeNotAllowed_ReturnsFailure()
    {
        // Arrange
        StorageBucket bucket = StorageBucket.Create(TenantId.New(), "test-bucket", allowedContentTypes: new[] { "image/*" });
        UploadFileCommand command = CreateCommand(contentType: "application/pdf");

        _bucketRepository.GetByNameAsync(command.BucketName, Arg.Any<CancellationToken>())
            .Returns(bucket);

        // Act
        Result<UploadResult> result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().StartWith("Validation");
        result.Error.Message.Should().Contain("Content type");
    }

    [Fact]
    public async Task Handle_WhenFileSizeExceeded_ReturnsFailure()
    {
        // Arrange
        StorageBucket bucket = StorageBucket.Create(TenantId.New(), "test-bucket", maxFileSizeBytes: 1000);
        UploadFileCommand command = CreateCommand(sizeBytes: 2000);

        _bucketRepository.GetByNameAsync(command.BucketName, Arg.Any<CancellationToken>())
            .Returns(bucket);

        // Act
        Result<UploadResult> result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().StartWith("Validation");
        result.Error.Message.Should().Contain("size");
    }

    [Fact]
    public async Task Handle_WhenValid_UploadsFileAndSavesMetadata()
    {
        // Arrange
        StorageBucket bucket = StorageBucket.Create(TenantId.New(), "test-bucket");
        UploadFileCommand command = CreateCommand();

        _bucketRepository.GetByNameAsync(command.BucketName, Arg.Any<CancellationToken>())
            .Returns(bucket);
        _storageProvider.UploadAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("etag-123");

        // Act
        Result<UploadResult> result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.FileName.Should().Be(command.FileName);
        result.Value.SizeBytes.Should().Be(command.SizeBytes);
        result.Value.ContentType.Should().Be(command.ContentType);

        await _storageProvider.Received(1).UploadAsync(
            Arg.Any<Stream>(),
            Arg.Is<string>(key => key.Contains($"tenant-{command.TenantId}")),
            command.ContentType,
            Arg.Any<CancellationToken>());

        _fileRepository.Received(1).Add(Arg.Any<StoredFile>());
        await _fileRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_StorageKeyFormat_IncludesTenantAndBucket()
    {
        // Arrange
        StorageBucket bucket = StorageBucket.Create(TenantId.New(), "invoices");
        Guid tenantId = Guid.NewGuid();
        UploadFileCommand command = CreateCommand(tenantId: tenantId, bucketName: "invoices", path: "2024/02");

        _bucketRepository.GetByNameAsync(command.BucketName, Arg.Any<CancellationToken>())
            .Returns(bucket);
        _storageProvider.UploadAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("etag");

        string? capturedKey = null;
        _storageProvider.When(x => x.UploadAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()))
            .Do(ci => capturedKey = ci.ArgAt<string>(1));

        // Act
        Result<UploadResult> result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        capturedKey.Should().NotBeNull();
        capturedKey.Should().StartWith($"tenant-{tenantId}");
        capturedKey.Should().Contain("invoices");
        capturedKey.Should().Contain("2024/02");
    }

    [Fact]
    public async Task Handle_WhenUploadingDuplicateFile_IsIdempotent()
    {
        // Arrange
        StorageBucket bucket = StorageBucket.Create(TenantId.New(), "test-bucket");
        UploadFileCommand command = CreateCommand();

        _bucketRepository.GetByNameAsync(command.BucketName, Arg.Any<CancellationToken>())
            .Returns(bucket);
        _storageProvider.UploadAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("etag-123");

        // Act
        Result<UploadResult> result1 = await _handler.Handle(command, CancellationToken.None);
        Result<UploadResult> result2 = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result1.IsSuccess.Should().BeTrue();
        result2.IsSuccess.Should().BeTrue();
        result1.Value.FileName.Should().Be(result2.Value.FileName);
        result1.Value.ContentType.Should().Be(result2.Value.ContentType);
        result1.Value.SizeBytes.Should().Be(result2.Value.SizeBytes);

        await _storageProvider.Received(2).UploadAsync(
            Arg.Any<Stream>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());

        _fileRepository.Received(2).Add(Arg.Any<StoredFile>());
        await _fileRepository.Received(2).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenFileScanFails_ReturnsFailureWithThreatName()
    {
        // Arrange
        StorageBucket bucket = StorageBucket.Create(TenantId.New(), "test-bucket");
        UploadFileCommand command = CreateCommand();

        _bucketRepository.GetByNameAsync(command.BucketName, Arg.Any<CancellationToken>())
            .Returns(bucket);
        _fileScanner.ScanAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(FileScanResult.Infected("Trojan.Generic"));

        // Act
        Result<UploadResult> result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().StartWith("Validation");
        result.Error.Message.Should().Contain("Trojan.Generic");

        await _storageProvider.DidNotReceive().UploadAsync(
            Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    private static UploadFileCommand CreateCommand(
        Guid? tenantId = null,
        string bucketName = "test-bucket",
        string fileName = "test-file.pdf",
        string contentType = "application/pdf",
        long sizeBytes = 1000,
        string? path = null)
    {
        return new UploadFileCommand(
            tenantId ?? Guid.NewGuid(),
            Guid.NewGuid(),
            bucketName,
            fileName,
            contentType,
            new MemoryStream([1, 2, 3]),
            sizeBytes,
            path);
    }
}
