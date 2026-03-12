using Foundry.Shared.Kernel.Identity;
using Foundry.Storage.Domain.Events;
using Foundry.Storage.Domain.Identity;

namespace Foundry.Storage.Tests.Domain.Entities;

public class StorageEventsTests
{
    [Fact]
    public void FileUploadedEvent_Construction_SetsAllProperties()
    {
        StoredFileId fileId = StoredFileId.New();
        StorageBucketId bucketId = StorageBucketId.New();
        TenantId tenantId = TenantId.New();

        FileUploadedEvent evt = new(fileId, bucketId, tenantId);

        evt.FileId.Should().Be(fileId);
        evt.BucketId.Should().Be(bucketId);
        evt.TenantId.Should().Be(tenantId);
    }

    [Fact]
    public void FileDeletedEvent_Construction_SetsAllProperties()
    {
        StoredFileId fileId = StoredFileId.New();
        StorageBucketId bucketId = StorageBucketId.New();
        TenantId tenantId = TenantId.New();

        FileDeletedEvent evt = new(fileId, bucketId, tenantId);

        evt.FileId.Should().Be(fileId);
        evt.BucketId.Should().Be(bucketId);
        evt.TenantId.Should().Be(tenantId);
    }

    [Fact]
    public void BucketCreatedEvent_Construction_SetsAllProperties()
    {
        StorageBucketId bucketId = StorageBucketId.New();

        BucketCreatedEvent evt = new(bucketId);

        evt.BucketId.Should().Be(bucketId);
    }

    [Fact]
    public void BucketDeletedEvent_Construction_SetsAllProperties()
    {
        StorageBucketId bucketId = StorageBucketId.New();

        BucketDeletedEvent evt = new(bucketId);

        evt.BucketId.Should().Be(bucketId);
    }

    [Fact]
    public void FileUploadedEvent_Construction_AssignsUniqueEventId()
    {
        FileUploadedEvent evt = new(StoredFileId.New(), StorageBucketId.New(), TenantId.New());

        evt.EventId.Should().NotBeEmpty();
    }

    [Fact]
    public void FileUploadedEvent_Construction_SetsOccurredAt()
    {
        DateTime before = DateTime.UtcNow;

        FileUploadedEvent evt = new(StoredFileId.New(), StorageBucketId.New(), TenantId.New());

        evt.OccurredAt.Should().BeOnOrAfter(before);
        evt.OccurredAt.Should().BeOnOrBefore(DateTime.UtcNow);
    }

    [Fact]
    public void FileDeletedEvent_Construction_AssignsUniqueEventId()
    {
        FileDeletedEvent evt = new(StoredFileId.New(), StorageBucketId.New(), TenantId.New());

        evt.EventId.Should().NotBeEmpty();
    }

    [Fact]
    public void BucketCreatedEvent_Construction_AssignsUniqueEventId()
    {
        BucketCreatedEvent evt = new(StorageBucketId.New());

        evt.EventId.Should().NotBeEmpty();
    }

    [Fact]
    public void BucketDeletedEvent_Construction_AssignsUniqueEventId()
    {
        BucketDeletedEvent evt = new(StorageBucketId.New());

        evt.EventId.Should().NotBeEmpty();
    }
}
