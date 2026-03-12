using Foundry.Shared.Kernel.Identity;
using Foundry.Storage.Domain.Entities;
using Foundry.Storage.Domain.Enums;
using Foundry.Storage.Domain.Events;
using Foundry.Storage.Domain.Identity;

namespace Foundry.Storage.Tests.Domain.Entities;

public class StoredFileStateTests
{
    [Fact]
    public void MarkAsAvailable_FromPendingValidation_SetsStatusToAvailable()
    {
        StoredFile file = CreatePendingFile();

        file.MarkAsAvailable();

        file.Status.Should().Be(FileStatus.Available);
    }

    [Fact]
    public void MarkAsRejected_FromPendingValidation_SetsStatusToRejected()
    {
        StoredFile file = CreatePendingFile();

        file.MarkAsRejected();

        file.Status.Should().Be(FileStatus.Rejected);
    }

    [Fact]
    public void MarkAsAvailable_FromAvailable_RemainsAvailable()
    {
        StoredFile file = CreateAvailableFile();

        file.MarkAsAvailable();

        file.Status.Should().Be(FileStatus.Available);
    }

    [Fact]
    public void MarkAsRejected_FromAvailable_SetsStatusToRejected()
    {
        StoredFile file = CreateAvailableFile();

        file.MarkAsRejected();

        file.Status.Should().Be(FileStatus.Rejected);
    }

    [Fact]
    public void MarkAsDeleted_RaisesFileDeletedDomainEvent()
    {
        StoredFile file = CreateAvailableFile();

        file.MarkAsDeleted();

        file.DomainEvents.Should().ContainSingle(e => e is FileDeletedEvent)
            .Which.Should().BeOfType<FileDeletedEvent>()
            .Which.Should().Match<FileDeletedEvent>(e =>
                e.FileId == file.Id &&
                e.BucketId == file.BucketId &&
                e.TenantId == file.TenantId);
    }

    [Fact]
    public void UpdateMetadata_WithNewValue_ChangesMetadata()
    {
        StoredFile file = CreateAvailableFile();
        string newMetadata = """{"category": "updated"}""";

        file.UpdateMetadata(newMetadata);

        file.Metadata.Should().Be(newMetadata);
    }

    [Fact]
    public void UpdateMetadata_WithNull_ClearsMetadata()
    {
        StoredFile file = CreateAvailableFile();
        file.UpdateMetadata("""{"key": "value"}""");

        file.UpdateMetadata(null);

        file.Metadata.Should().BeNull();
    }

    [Fact]
    public void SetPublic_WithTrue_SetsIsPublicToTrue()
    {
        StoredFile file = CreateAvailableFile();

        file.SetPublic(true);

        file.IsPublic.Should().BeTrue();
    }

    [Fact]
    public void SetPublic_WithFalse_SetsIsPublicToFalse()
    {
        StoredFile file = CreateAvailableFile();
        file.SetPublic(true);

        file.SetPublic(false);

        file.IsPublic.Should().BeFalse();
    }

    private static StoredFile CreatePendingFile()
    {
        return StoredFile.CreatePendingValidation(
            TenantId.New(),
            StorageBucketId.New(),
            "test.txt",
            "text/plain",
            100,
            "key/test.txt",
            Guid.NewGuid());
    }

    private static StoredFile CreateAvailableFile()
    {
        return StoredFile.Create(
            TenantId.New(),
            StorageBucketId.New(),
            "test.txt",
            "text/plain",
            100,
            "key/test.txt",
            Guid.NewGuid());
    }
}
