using Foundry.Shared.Kernel.Identity;
using Foundry.Storage.Application.DTOs;
using Foundry.Storage.Application.Mappings;
using Foundry.Storage.Domain.Entities;
using Foundry.Storage.Domain.Enums;
using Foundry.Storage.Domain.ValueObjects;

#pragma warning disable CA1861 // Inline arrays in test data initializers

namespace Foundry.Storage.Tests.Application;

public class StorageMappingsTests
{
    [Fact]
    public void ToDto_StoredFile_MapsAllProperties()
    {
        TenantId tenantId = TenantId.New();
        StorageBucket bucket = StorageBucket.Create(tenantId, "bucket");
        Guid uploadedBy = Guid.NewGuid();
        StoredFile file = StoredFile.Create(
            tenantId, bucket.Id, "report.pdf", "application/pdf", 5000,
            "tenant-123/bucket/report.pdf", uploadedBy, "reports", true, "{\"key\":\"value\"}");

        StoredFileDto dto = file.ToDto();

        dto.Id.Should().Be(file.Id.Value);
        dto.TenantId.Should().Be(tenantId.Value);
        dto.BucketId.Should().Be(bucket.Id.Value);
        dto.FileName.Should().Be("report.pdf");
        dto.ContentType.Should().Be("application/pdf");
        dto.SizeBytes.Should().Be(5000);
        dto.Path.Should().Be("reports");
        dto.IsPublic.Should().BeTrue();
        dto.UploadedBy.Should().Be(uploadedBy);
        dto.UploadedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
        dto.Metadata.Should().Be("{\"key\":\"value\"}");
    }

    [Fact]
    public void ToDto_StoredFile_WithNullOptionalFields_MapsCorrectly()
    {
        TenantId tenantId = TenantId.New();
        StorageBucket bucket = StorageBucket.Create(tenantId, "bucket");
        StoredFile file = StoredFile.Create(
            tenantId, bucket.Id, "file.txt", "text/plain", 100, "key", Guid.NewGuid());

        StoredFileDto dto = file.ToDto();

        dto.Path.Should().BeNull();
        dto.IsPublic.Should().BeFalse();
        dto.Metadata.Should().BeNull();
    }

    [Fact]
    public void ToDto_StorageBucket_MapsAllProperties()
    {
        TenantId tenantId = TenantId.New();
        RetentionPolicy retention = new(90, RetentionAction.Archive);
        StorageBucket bucket = StorageBucket.Create(
            tenantId, "my-bucket", "A description", AccessLevel.Public,
            1024 * 1024, new[] { "image/png", "image/jpeg" }, retention, true);

        BucketDto dto = bucket.ToDto();

        dto.Id.Should().Be(bucket.Id.Value);
        dto.Name.Should().Be("my-bucket");
        dto.Description.Should().Be("A description");
        dto.Access.Should().Be("Public");
        dto.MaxFileSizeBytes.Should().Be(1024 * 1024);
        dto.AllowedContentTypes.Should().Contain("image/png");
        dto.AllowedContentTypes.Should().Contain("image/jpeg");
        dto.Versioning.Should().BeTrue();
        dto.Retention.Should().NotBeNull();
        dto.Retention!.Days.Should().Be(90);
        dto.Retention.Action.Should().Be("Archive");
        dto.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void ToDto_StorageBucket_WithNoRetentionOrContentTypes_MapsCorrectly()
    {
        StorageBucket bucket = StorageBucket.Create(TenantId.New(), "simple");

        BucketDto dto = bucket.ToDto();

        dto.Name.Should().Be("simple");
        dto.Description.Should().BeNull();
        dto.Access.Should().Be("Private");
        dto.MaxFileSizeBytes.Should().Be(0);
        dto.AllowedContentTypes.Should().BeNull();
        dto.Retention.Should().BeNull();
        dto.Versioning.Should().BeFalse();
    }
}
