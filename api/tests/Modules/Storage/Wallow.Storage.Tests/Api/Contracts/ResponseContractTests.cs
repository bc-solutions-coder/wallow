using Wallow.Storage.Api.Contracts.Responses;

namespace Wallow.Storage.Tests.Api.Contracts;

public class ResponseContractTests
{
    #region BucketResponse

    [Fact]
    public void BucketResponse_CreatesWithAllFields()
    {
        Guid id = Guid.NewGuid();
        DateTime createdAt = DateTime.UtcNow;
        List<string> contentTypes = ["image/png", "image/jpeg"];
        RetentionPolicyResponse retention = new(30, "Delete");

        BucketResponse response = new(id, "bucket-name", "desc", "Public", 1024, contentTypes, retention, true, createdAt);

        response.Id.Should().Be(id);
        response.Name.Should().Be("bucket-name");
        response.Description.Should().Be("desc");
        response.Access.Should().Be("Public");
        response.MaxFileSizeBytes.Should().Be(1024);
        response.AllowedContentTypes.Should().HaveCount(2);
        response.Retention.Should().NotBeNull();
        response.Retention!.Days.Should().Be(30);
        response.Retention.Action.Should().Be("Delete");
        response.Versioning.Should().BeTrue();
        response.CreatedAt.Should().Be(createdAt);
    }

    [Fact]
    public void BucketResponse_WithNullOptionalFields_CreatesCorrectly()
    {
        BucketResponse response = new(Guid.NewGuid(), "bucket", null, "Private", 0, null, null, false, DateTime.UtcNow);

        response.Description.Should().BeNull();
        response.AllowedContentTypes.Should().BeNull();
        response.Retention.Should().BeNull();
    }

    #endregion

    #region RetentionPolicyResponse

    [Fact]
    public void RetentionPolicyResponse_CreatesWithAllFields()
    {
        RetentionPolicyResponse response = new(90, "Archive");

        response.Days.Should().Be(90);
        response.Action.Should().Be("Archive");
    }

    #endregion

    #region FileMetadataResponse

    [Fact]
    public void FileMetadataResponse_CreatesWithAllFields()
    {
        Guid id = Guid.NewGuid();
        Guid bucketId = Guid.NewGuid();
        Guid uploadedBy = Guid.NewGuid();
        DateTime uploadedAt = DateTime.UtcNow;

        FileMetadataResponse response = new(id, bucketId, "file.txt", "text/plain", 2048, "docs/", true, uploadedBy, uploadedAt);

        response.Id.Should().Be(id);
        response.BucketId.Should().Be(bucketId);
        response.FileName.Should().Be("file.txt");
        response.ContentType.Should().Be("text/plain");
        response.SizeBytes.Should().Be(2048);
        response.Path.Should().Be("docs/");
        response.IsPublic.Should().BeTrue();
        response.UploadedBy.Should().Be(uploadedBy);
        response.UploadedAt.Should().Be(uploadedAt);
    }

    [Fact]
    public void FileMetadataResponse_WithNullPath_CreatesCorrectly()
    {
        FileMetadataResponse response = new(Guid.NewGuid(), Guid.NewGuid(), "file.txt", "text/plain", 100, null, false, Guid.NewGuid(), DateTime.UtcNow);

        response.Path.Should().BeNull();
    }

    #endregion

    #region UploadResponse

    [Fact]
    public void UploadResponse_CreatesWithAllFields()
    {
        Guid fileId = Guid.NewGuid();
        DateTime uploadedAt = DateTime.UtcNow;

        UploadResponse response = new(fileId, "report.pdf", 5000, "application/pdf", uploadedAt);

        response.FileId.Should().Be(fileId);
        response.FileName.Should().Be("report.pdf");
        response.SizeBytes.Should().Be(5000);
        response.ContentType.Should().Be("application/pdf");
        response.UploadedAt.Should().Be(uploadedAt);
    }

    #endregion

    #region PresignedUploadResponse

    [Fact]
    public void PresignedUploadResponse_CreatesWithAllFields()
    {
        DateTime expiresAt = DateTime.UtcNow.AddHours(1);

        Guid fileId = Guid.NewGuid();
        PresignedUploadResponse response = new(fileId, "https://storage.example.com/upload", expiresAt);

        response.FileId.Should().Be(fileId);
        response.UploadUrl.Should().Be("https://storage.example.com/upload");
        response.ExpiresAt.Should().Be(expiresAt);
    }

    #endregion

    #region PresignedUrlResponse

    [Fact]
    public void PresignedUrlResponse_CreatesWithAllFields()
    {
        DateTime expiresAt = DateTime.UtcNow.AddMinutes(30);

        PresignedUrlResponse response = new("https://storage.example.com/download?token=abc", expiresAt);

        response.Url.Should().Be("https://storage.example.com/download?token=abc");
        response.ExpiresAt.Should().Be(expiresAt);
    }

    #endregion
}
