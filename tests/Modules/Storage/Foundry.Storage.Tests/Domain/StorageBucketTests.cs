using Foundry.Shared.Kernel.Identity;
using Foundry.Storage.Domain.Entities;
using Foundry.Storage.Domain.Enums;
using Foundry.Storage.Domain.ValueObjects;

#pragma warning disable CA1861 // Inline arrays in test data initializers

namespace Foundry.Storage.Tests.Domain;

public class StorageBucketTests
{
    [Fact]
    public void Create_ShouldSetAllProperties()
    {
        // Arrange
        string name = "test-bucket";
        string description = "Test description";
        string[] allowedTypes = ["image/*", "application/pdf"];

        // Act
        TenantId tenantId = TenantId.New();
        StorageBucket bucket = StorageBucket.Create(
            tenantId,
            name,
            description,
            AccessLevel.Public,
            10 * 1024 * 1024, // 10MB
            allowedTypes,
            new RetentionPolicy(30, RetentionAction.Delete),
            versioning: true);

        // Assert
        bucket.Id.Value.Should().NotBeEmpty();
        bucket.Name.Should().Be(name);
        bucket.Description.Should().Be(description);
        bucket.Access.Should().Be(AccessLevel.Public);
        bucket.MaxFileSizeBytes.Should().Be(10 * 1024 * 1024);
        bucket.Versioning.Should().BeTrue();
        bucket.Retention.Should().NotBeNull();
        bucket.Retention!.Days.Should().Be(30);
        bucket.Retention.Action.Should().Be(RetentionAction.Delete);
    }

    [Theory]
    [InlineData("image/png", true)]
    [InlineData("image/jpeg", true)]
    [InlineData("image/gif", true)]
    [InlineData("application/pdf", false)]
    [InlineData("text/plain", false)]
    public void IsContentTypeAllowed_WithWildcard_ShouldMatchCorrectly(string contentType, bool expected)
    {
        // Arrange
        StorageBucket bucket = StorageBucket.Create(TenantId.New(), "test", allowedContentTypes: ["image/*"]);

        // Act
        bool result = bucket.IsContentTypeAllowed(contentType);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void IsContentTypeAllowed_WithNullAllowed_ShouldAllowAll()
    {
        // Arrange
        StorageBucket bucket = StorageBucket.Create(TenantId.New(), "test");

        // Act & Assert
        bucket.IsContentTypeAllowed("image/png").Should().BeTrue();
        bucket.IsContentTypeAllowed("application/pdf").Should().BeTrue();
        bucket.IsContentTypeAllowed("video/mp4").Should().BeTrue();
    }

    [Fact]
    public void IsContentTypeAllowed_WithExactMatch_ShouldWork()
    {
        // Arrange
        StorageBucket bucket = StorageBucket.Create(TenantId.New(), "test", allowedContentTypes: ["application/pdf", "application/json"]);

        // Act & Assert
        bucket.IsContentTypeAllowed("application/pdf").Should().BeTrue();
        bucket.IsContentTypeAllowed("application/json").Should().BeTrue();
        bucket.IsContentTypeAllowed("application/xml").Should().BeFalse();
    }

    [Theory]
    [InlineData(0, 1000, true)] // No limit
    [InlineData(1000, 500, true)] // Under limit
    [InlineData(1000, 1000, true)] // At limit
    [InlineData(1000, 1001, false)] // Over limit
    public void IsFileSizeAllowed_ShouldEnforceLimit(long maxSize, long fileSize, bool expected)
    {
        // Arrange
        StorageBucket bucket = StorageBucket.Create(TenantId.New(), "test", maxFileSizeBytes: maxSize);

        // Act
        bool result = bucket.IsFileSizeAllowed(fileSize);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void IsContentTypeAllowed_ShouldBeCaseInsensitive()
    {
        // Arrange
        StorageBucket bucket = StorageBucket.Create(TenantId.New(), "test", allowedContentTypes: ["Image/*"]);

        // Act & Assert
        bucket.IsContentTypeAllowed("IMAGE/PNG").Should().BeTrue();
        bucket.IsContentTypeAllowed("image/png").Should().BeTrue();
        bucket.IsContentTypeAllowed("Image/PNG").Should().BeTrue();
    }

    [Theory]
    [InlineData("*/*")]
    [InlineData("*")]
    public void IsContentTypeAllowed_WithUniversalWildcard_ShouldAllowAll(string pattern)
    {
        StorageBucket bucket = StorageBucket.Create(TenantId.New(), "test", allowedContentTypes: [pattern]);

        bucket.IsContentTypeAllowed("image/png").Should().BeTrue();
        bucket.IsContentTypeAllowed("application/pdf").Should().BeTrue();
        bucket.IsContentTypeAllowed("text/plain").Should().BeTrue();
    }

    [Fact]
    public void IsContentTypeAllowed_WithEmptyAllowedList_ShouldAllowAll()
    {
        StorageBucket bucket = StorageBucket.Create(TenantId.New(), "test", allowedContentTypes: []);

        bucket.IsContentTypeAllowed("image/png").Should().BeTrue();
        bucket.IsContentTypeAllowed("application/pdf").Should().BeTrue();
    }

    [Fact]
    public void UpdateDescription_ShouldChangeDescription()
    {
        StorageBucket bucket = StorageBucket.Create(TenantId.New(), "test");

        bucket.UpdateDescription("new description");

        bucket.Description.Should().Be("new description");
    }

    [Fact]
    public void UpdateAccess_ShouldChangeAccessLevel()
    {
        StorageBucket bucket = StorageBucket.Create(TenantId.New(), "test");

        bucket.UpdateAccess(AccessLevel.Public);

        bucket.Access.Should().Be(AccessLevel.Public);
    }

    [Fact]
    public void UpdateMaxFileSize_ShouldChangeMaxFileSizeBytes()
    {
        StorageBucket bucket = StorageBucket.Create(TenantId.New(), "test");

        bucket.UpdateMaxFileSize(5_000_000);

        bucket.MaxFileSizeBytes.Should().Be(5_000_000);
    }

    [Fact]
    public void UpdateAllowedContentTypes_ShouldChangeAllowedTypes()
    {
        StorageBucket bucket = StorageBucket.Create(TenantId.New(), "test");

        bucket.UpdateAllowedContentTypes(["image/png", "image/jpeg"]);

        bucket.IsContentTypeAllowed("image/png").Should().BeTrue();
        bucket.IsContentTypeAllowed("image/jpeg").Should().BeTrue();
        bucket.IsContentTypeAllowed("application/pdf").Should().BeFalse();
    }

    [Fact]
    public void UpdateAllowedContentTypes_WithNull_ShouldAllowAll()
    {
        StorageBucket bucket = StorageBucket.Create(TenantId.New(), "test", allowedContentTypes: ["image/png"]);

        bucket.UpdateAllowedContentTypes(null);

        bucket.IsContentTypeAllowed("application/pdf").Should().BeTrue();
    }

    [Fact]
    public void UpdateRetention_ShouldChangeRetentionPolicy()
    {
        StorageBucket bucket = StorageBucket.Create(TenantId.New(), "test");

        RetentionPolicy policy = new(90, RetentionAction.Archive);
        bucket.UpdateRetention(policy);

        bucket.Retention.Should().Be(policy);
    }

    [Fact]
    public void UpdateVersioning_ShouldChangeVersioningFlag()
    {
        StorageBucket bucket = StorageBucket.Create(TenantId.New(), "test");

        bucket.UpdateVersioning(true);

        bucket.Versioning.Should().BeTrue();
    }
}
