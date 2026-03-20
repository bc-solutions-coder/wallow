using Wallow.Shared.Kernel.Identity;
using Wallow.Storage.Domain.Entities;
using Wallow.Storage.Domain.Enums;
using Wallow.Storage.Domain.Events;
using Wallow.Storage.Domain.Identity;

namespace Wallow.Storage.Tests.Domain.Entities;

public class StoredFileCreateTests
{
    [Fact]
    public void Create_WithValidData_SetsAllProperties()
    {
        TenantId tenantId = TenantId.New();
        StorageBucketId bucketId = StorageBucketId.New();
        string fileName = "test-file.pdf";
        string contentType = "application/pdf";
        long sizeBytes = 12345L;
        string storageKey = $"tenant-{tenantId.Value}/invoices/{Guid.NewGuid()}.pdf";
        Guid uploadedBy = Guid.NewGuid();
        string path = "invoices/2024";
        string metadata = """{"category": "invoice"}""";

        StoredFile file = StoredFile.Create(
            tenantId,
            bucketId,
            fileName,
            contentType,
            sizeBytes,
            storageKey,
            uploadedBy,
            path,
            isPublic: true,
            metadata);

        file.Id.Value.Should().NotBeEmpty();
        file.TenantId.Should().Be(tenantId);
        file.BucketId.Should().Be(bucketId);
        file.FileName.Should().Be(fileName);
        file.ContentType.Should().Be(contentType);
        file.SizeBytes.Should().Be(sizeBytes);
        file.StorageKey.Should().Be(storageKey);
        file.Path.Should().Be(path);
        file.IsPublic.Should().BeTrue();
        file.UploadedBy.Should().Be(uploadedBy);
        file.UploadedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        file.Metadata.Should().Be(metadata);
    }

    [Fact]
    public void Create_WithValidData_SetsStatusToAvailable()
    {
        StoredFile file = CreateTestFile();

        file.Status.Should().Be(FileStatus.Available);
    }

    [Fact]
    public void Create_WithDefaultOptionalParameters_UsesDefaults()
    {
        TenantId tenantId = TenantId.New();
        StorageBucketId bucketId = StorageBucketId.New();
        Guid uploadedBy = Guid.NewGuid();

        StoredFile file = StoredFile.Create(
            tenantId,
            bucketId,
            "test.txt",
            "text/plain",
            100,
            "key/test.txt",
            uploadedBy);

        file.Path.Should().BeNull();
        file.IsPublic.Should().BeFalse();
        file.Metadata.Should().BeNull();
    }

    [Fact]
    public void Create_GeneratesUniqueId()
    {
        StoredFile file1 = CreateTestFile();
        StoredFile file2 = CreateTestFile();

        file1.Id.Should().NotBe(file2.Id);
    }

    [Fact]
    public void Create_SetsUploadedAtToCurrentUtcTime()
    {
        DateTime before = DateTime.UtcNow;

        StoredFile file = CreateTestFile();

        DateTime after = DateTime.UtcNow;
        file.UploadedAt.Should().BeOnOrAfter(before);
        file.UploadedAt.Should().BeOnOrBefore(after);
    }

    [Fact]
    public void Create_RaisesFileUploadedDomainEvent()
    {
        TenantId tenantId = TenantId.New();
        StorageBucketId bucketId = StorageBucketId.New();

        StoredFile file = StoredFile.Create(
            tenantId,
            bucketId,
            "test.txt",
            "text/plain",
            100,
            "key/test.txt",
            Guid.NewGuid());

        file.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<FileUploadedEvent>()
            .Which.Should().Match<FileUploadedEvent>(e =>
                e.FileId == file.Id &&
                e.BucketId == bucketId &&
                e.TenantId == tenantId);
    }

    [Fact]
    public void CreatePendingValidation_WithValidData_SetsStatusToPendingValidation()
    {
        StoredFile file = StoredFile.CreatePendingValidation(
            TenantId.New(),
            StorageBucketId.New(),
            "test.txt",
            "text/plain",
            100,
            "key/test.txt",
            Guid.NewGuid());

        file.Status.Should().Be(FileStatus.PendingValidation);
    }

    [Fact]
    public void CreatePendingValidation_WithValidData_SetsAllProperties()
    {
        TenantId tenantId = TenantId.New();
        StorageBucketId bucketId = StorageBucketId.New();
        string fileName = "scan-me.pdf";
        string contentType = "application/pdf";
        long sizeBytes = 5000L;
        string storageKey = "key/scan-me.pdf";
        Guid uploadedBy = Guid.NewGuid();
        string path = "uploads";
        string metadata = """{"scan": true}""";

        StoredFile file = StoredFile.CreatePendingValidation(
            tenantId,
            bucketId,
            fileName,
            contentType,
            sizeBytes,
            storageKey,
            uploadedBy,
            path,
            isPublic: true,
            metadata);

        file.Id.Value.Should().NotBeEmpty();
        file.TenantId.Should().Be(tenantId);
        file.BucketId.Should().Be(bucketId);
        file.FileName.Should().Be(fileName);
        file.ContentType.Should().Be(contentType);
        file.SizeBytes.Should().Be(sizeBytes);
        file.StorageKey.Should().Be(storageKey);
        file.Path.Should().Be(path);
        file.IsPublic.Should().BeTrue();
        file.UploadedBy.Should().Be(uploadedBy);
        file.Metadata.Should().Be(metadata);
    }

    [Fact]
    public void CreatePendingValidation_DoesNotRaiseDomainEvent()
    {
        StoredFile file = StoredFile.CreatePendingValidation(
            TenantId.New(),
            StorageBucketId.New(),
            "test.txt",
            "text/plain",
            100,
            "key/test.txt",
            Guid.NewGuid());

        file.DomainEvents.Should().BeEmpty();
    }

    private static StoredFile CreateTestFile()
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
