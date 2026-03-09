using Foundry.Shared.Kernel.Identity;
using Foundry.Shared.Kernel.Results;
using Foundry.Storage.Application.DTOs;
using Foundry.Storage.Application.Interfaces;
using Foundry.Storage.Application.Queries.GetFileById;
using Foundry.Storage.Domain.Entities;
using Foundry.Storage.Domain.Identity;

namespace Foundry.Storage.Tests.Application;

public class GetFileByIdHandlerTests
{
    private readonly IStoredFileRepository _fileRepository;
    private readonly GetFileByIdHandler _handler;

    public GetFileByIdHandlerTests()
    {
        _fileRepository = Substitute.For<IStoredFileRepository>();
        _handler = new GetFileByIdHandler(_fileRepository);
    }

    [Fact]
    public async Task Handle_WhenFileExistsAndTenantMatches_ReturnsSuccess()
    {
        TenantId tenantId = TenantId.New();
        StorageBucket bucket = StorageBucket.Create(tenantId, "bucket");
        StoredFile file = StoredFile.Create(
            tenantId, bucket.Id, "test.pdf", "application/pdf", 5000, "key/test.pdf", Guid.NewGuid());
        GetFileByIdQuery query = new(tenantId.Value, file.Id.Value);

        _fileRepository.GetByIdAsync(Arg.Any<StoredFileId>(), Arg.Any<CancellationToken>())
            .Returns(file);

        Result<StoredFileDto> result = await _handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.FileName.Should().Be("test.pdf");
        result.Value.ContentType.Should().Be("application/pdf");
        result.Value.SizeBytes.Should().Be(5000);
    }

    [Fact]
    public async Task Handle_WhenFileNotFound_ReturnsNotFoundFailure()
    {
        Guid tenantId = Guid.NewGuid();
        Guid fileId = Guid.NewGuid();
        GetFileByIdQuery query = new(tenantId, fileId);

        _fileRepository.GetByIdAsync(Arg.Any<StoredFileId>(), Arg.Any<CancellationToken>())
            .Returns((StoredFile?)null);

        Result<StoredFileDto> result = await _handler.Handle(query, CancellationToken.None);

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
            fileTenantId, bucket.Id, "test.pdf", "application/pdf", 5000, "key/test.pdf", Guid.NewGuid());
        GetFileByIdQuery query = new(differentTenantId, file.Id.Value);

        _fileRepository.GetByIdAsync(Arg.Any<StoredFileId>(), Arg.Any<CancellationToken>())
            .Returns(file);

        Result<StoredFileDto> result = await _handler.Handle(query, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Contain("NotFound");
    }
}
