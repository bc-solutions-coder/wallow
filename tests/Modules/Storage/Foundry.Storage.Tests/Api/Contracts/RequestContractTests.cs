using Foundry.Storage.Api.Contracts.Requests;

namespace Foundry.Storage.Tests.Api.Contracts;

public class RequestContractTests
{
    #region CreateBucketRequest

    [Fact]
    public void CreateBucketRequest_WithRequiredFields_CreatesInstance()
    {
        CreateBucketRequest request = new("my-bucket");

        request.Name.Should().Be("my-bucket");
        request.Description.Should().BeNull();
        request.Access.Should().Be("Private");
        request.MaxFileSizeBytes.Should().Be(0);
        request.AllowedContentTypes.Should().BeNull();
        request.RetentionDays.Should().BeNull();
        request.RetentionAction.Should().BeNull();
        request.Versioning.Should().BeFalse();
    }

    [Fact]
    public void CreateBucketRequest_WithAllFields_CreatesInstance()
    {
        List<string> contentTypes = ["image/png"];
        CreateBucketRequest request = new(
            "my-bucket",
            "A description",
            "Public",
            1024 * 1024,
            contentTypes,
            30,
            "Archive",
            true);

        request.Name.Should().Be("my-bucket");
        request.Description.Should().Be("A description");
        request.Access.Should().Be("Public");
        request.MaxFileSizeBytes.Should().Be(1024 * 1024);
        request.AllowedContentTypes.Should().ContainSingle().Which.Should().Be("image/png");
        request.RetentionDays.Should().Be(30);
        request.RetentionAction.Should().Be("Archive");
        request.Versioning.Should().BeTrue();
    }

    #endregion

    #region PresignedUploadRequest

    [Fact]
    public void PresignedUploadRequest_WithRequiredFields_CreatesInstance()
    {
        PresignedUploadRequest request = new("test-bucket", "file.txt", "text/plain", 1024);

        request.BucketName.Should().Be("test-bucket");
        request.FileName.Should().Be("file.txt");
        request.ContentType.Should().Be("text/plain");
        request.SizeBytes.Should().Be(1024);
        request.Path.Should().BeNull();
        request.ExpiryMinutes.Should().BeNull();
    }

    [Fact]
    public void PresignedUploadRequest_WithAllFields_CreatesInstance()
    {
        PresignedUploadRequest request = new(
            "test-bucket",
            "file.txt",
            "text/plain",
            1024,
            "uploads/",
            60);

        request.BucketName.Should().Be("test-bucket");
        request.FileName.Should().Be("file.txt");
        request.ContentType.Should().Be("text/plain");
        request.SizeBytes.Should().Be(1024);
        request.Path.Should().Be("uploads/");
        request.ExpiryMinutes.Should().Be(60);
    }

    #endregion
}
