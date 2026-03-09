using Foundry.Shared.Kernel.Identity;
using Foundry.Storage.Domain.Entities;
using Foundry.Storage.Domain.Enums;
using Foundry.Storage.Domain.ValueObjects;

#pragma warning disable CA1861 // Inline arrays in test data initializers

namespace Foundry.Storage.Tests.Domain.Entities;

public class StorageBucketCreateTests
{
    [Fact]
    public void Create_WithAllParameters_SetsAllProperties()
    {
        TenantId tenantId = TenantId.New();
        string name = "test-bucket";
        string description = "Test description";
        string[] allowedTypes = ["image/*", "application/pdf"];
        RetentionPolicy retention = new(30, RetentionAction.Delete);

        StorageBucket bucket = StorageBucket.Create(
            tenantId,
            name,
            description,
            AccessLevel.Public,
            10 * 1024 * 1024,
            allowedTypes,
            retention,
            versioning: true);

        bucket.Id.Value.Should().NotBeEmpty();
        bucket.TenantId.Should().Be(tenantId);
        bucket.Name.Should().Be(name);
        bucket.Description.Should().Be(description);
        bucket.Access.Should().Be(AccessLevel.Public);
        bucket.MaxFileSizeBytes.Should().Be(10 * 1024 * 1024);
        bucket.Versioning.Should().BeTrue();
        bucket.Retention.Should().NotBeNull();
        bucket.Retention!.Days.Should().Be(30);
        bucket.Retention.Action.Should().Be(RetentionAction.Delete);
    }

    [Fact]
    public void Create_WithDefaultParameters_SetsDefaults()
    {
        TenantId tenantId = TenantId.New();

        StorageBucket bucket = StorageBucket.Create(tenantId, "my-bucket");

        bucket.Name.Should().Be("my-bucket");
        bucket.Description.Should().BeNull();
        bucket.Access.Should().Be(AccessLevel.Private);
        bucket.MaxFileSizeBytes.Should().Be(0);
        bucket.AllowedContentTypes.Should().BeNull();
        bucket.Retention.Should().BeNull();
        bucket.Versioning.Should().BeFalse();
    }

    [Fact]
    public void Create_WithContentTypes_SerializesAsJson()
    {
        StorageBucket bucket = StorageBucket.Create(
            TenantId.New(),
            "test",
            allowedContentTypes: ["image/png", "application/pdf"]);

        bucket.AllowedContentTypes.Should().NotBeNull();
        bucket.IsContentTypeAllowed("image/png").Should().BeTrue();
        bucket.IsContentTypeAllowed("application/pdf").Should().BeTrue();
    }
}

public class StorageBucketIsContentTypeAllowedTests
{
    [Fact]
    public void IsContentTypeAllowed_WithNullAllowedTypes_ReturnsTrue()
    {
        StorageBucket bucket = StorageBucket.Create(TenantId.New(), "test");

        bucket.IsContentTypeAllowed("image/png").Should().BeTrue();
        bucket.IsContentTypeAllowed("application/pdf").Should().BeTrue();
        bucket.IsContentTypeAllowed("video/mp4").Should().BeTrue();
    }

    [Fact]
    public void IsContentTypeAllowed_WithEmptyAllowedList_ReturnsTrue()
    {
        StorageBucket bucket = StorageBucket.Create(TenantId.New(), "test", allowedContentTypes: []);

        bucket.IsContentTypeAllowed("image/png").Should().BeTrue();
        bucket.IsContentTypeAllowed("application/pdf").Should().BeTrue();
    }

    [Theory]
    [InlineData("image/png", true)]
    [InlineData("image/jpeg", true)]
    [InlineData("image/gif", true)]
    [InlineData("application/pdf", false)]
    [InlineData("text/plain", false)]
    public void IsContentTypeAllowed_WithWildcardPattern_MatchesCorrectly(string contentType, bool expected)
    {
        StorageBucket bucket = StorageBucket.Create(TenantId.New(), "test", allowedContentTypes: ["image/*"]);

        bool result = bucket.IsContentTypeAllowed(contentType);

        result.Should().Be(expected);
    }

    [Fact]
    public void IsContentTypeAllowed_WithExactMatch_ReturnsCorrectResult()
    {
        StorageBucket bucket = StorageBucket.Create(TenantId.New(), "test", allowedContentTypes: ["application/pdf", "application/json"]);

        bucket.IsContentTypeAllowed("application/pdf").Should().BeTrue();
        bucket.IsContentTypeAllowed("application/json").Should().BeTrue();
        bucket.IsContentTypeAllowed("application/xml").Should().BeFalse();
    }

    [Fact]
    public void IsContentTypeAllowed_WithDifferentCasing_ReturnsTrueCaseInsensitive()
    {
        StorageBucket bucket = StorageBucket.Create(TenantId.New(), "test", allowedContentTypes: ["Image/*"]);

        bucket.IsContentTypeAllowed("IMAGE/PNG").Should().BeTrue();
        bucket.IsContentTypeAllowed("image/png").Should().BeTrue();
        bucket.IsContentTypeAllowed("Image/PNG").Should().BeTrue();
    }

    [Theory]
    [InlineData("*/*")]
    [InlineData("*")]
    public void IsContentTypeAllowed_WithUniversalWildcard_ReturnsTrue(string pattern)
    {
        StorageBucket bucket = StorageBucket.Create(TenantId.New(), "test", allowedContentTypes: [pattern]);

        bucket.IsContentTypeAllowed("image/png").Should().BeTrue();
        bucket.IsContentTypeAllowed("application/pdf").Should().BeTrue();
        bucket.IsContentTypeAllowed("text/plain").Should().BeTrue();
    }
}

public class StorageBucketIsFileSizeAllowedTests
{
    [Theory]
    [InlineData(0, 1000, true)]
    [InlineData(1000, 500, true)]
    [InlineData(1000, 1000, true)]
    [InlineData(1000, 1001, false)]
    public void IsFileSizeAllowed_WithVariousLimits_EnforcesCorrectly(long maxSize, long fileSize, bool expected)
    {
        StorageBucket bucket = StorageBucket.Create(TenantId.New(), "test", maxFileSizeBytes: maxSize);

        bool result = bucket.IsFileSizeAllowed(fileSize);

        result.Should().Be(expected);
    }

    [Fact]
    public void IsFileSizeAllowed_WithZeroMaxSize_AllowsAnySize()
    {
        StorageBucket bucket = StorageBucket.Create(TenantId.New(), "test", maxFileSizeBytes: 0);

        bucket.IsFileSizeAllowed(long.MaxValue).Should().BeTrue();
    }
}

public class StorageBucketUpdateTests
{
    [Fact]
    public void UpdateDescription_WithNewValue_ChangesDescription()
    {
        StorageBucket bucket = StorageBucket.Create(TenantId.New(), "test");

        bucket.UpdateDescription("new description");

        bucket.Description.Should().Be("new description");
    }

    [Fact]
    public void UpdateDescription_WithNull_ClearsDescription()
    {
        StorageBucket bucket = StorageBucket.Create(TenantId.New(), "test", description: "original");

        bucket.UpdateDescription(null);

        bucket.Description.Should().BeNull();
    }

    [Fact]
    public void UpdateAccess_WithPublic_ChangesAccessLevel()
    {
        StorageBucket bucket = StorageBucket.Create(TenantId.New(), "test");

        bucket.UpdateAccess(AccessLevel.Public);

        bucket.Access.Should().Be(AccessLevel.Public);
    }

    [Fact]
    public void UpdateMaxFileSize_WithNewValue_ChangesLimit()
    {
        StorageBucket bucket = StorageBucket.Create(TenantId.New(), "test");

        bucket.UpdateMaxFileSize(5_000_000);

        bucket.MaxFileSizeBytes.Should().Be(5_000_000);
    }

    [Fact]
    public void UpdateAllowedContentTypes_WithNewTypes_ChangesAllowedTypes()
    {
        StorageBucket bucket = StorageBucket.Create(TenantId.New(), "test");

        bucket.UpdateAllowedContentTypes(["image/png", "image/jpeg"]);

        bucket.IsContentTypeAllowed("image/png").Should().BeTrue();
        bucket.IsContentTypeAllowed("image/jpeg").Should().BeTrue();
        bucket.IsContentTypeAllowed("application/pdf").Should().BeFalse();
    }

    [Fact]
    public void UpdateAllowedContentTypes_WithNull_AllowsAllTypes()
    {
        StorageBucket bucket = StorageBucket.Create(TenantId.New(), "test", allowedContentTypes: ["image/png"]);

        bucket.UpdateAllowedContentTypes(null);

        bucket.IsContentTypeAllowed("application/pdf").Should().BeTrue();
    }

    [Fact]
    public void UpdateRetention_WithNewPolicy_ChangesRetention()
    {
        StorageBucket bucket = StorageBucket.Create(TenantId.New(), "test");
        RetentionPolicy policy = new(90, RetentionAction.Archive);

        bucket.UpdateRetention(policy);

        bucket.Retention.Should().Be(policy);
    }

    [Fact]
    public void UpdateRetention_WithNull_ClearsRetention()
    {
        RetentionPolicy original = new(30, RetentionAction.Delete);
        StorageBucket bucket = StorageBucket.Create(TenantId.New(), "test", retention: original);

        bucket.UpdateRetention(null);

        bucket.Retention.Should().BeNull();
    }

    [Fact]
    public void UpdateVersioning_WithTrue_EnablesVersioning()
    {
        StorageBucket bucket = StorageBucket.Create(TenantId.New(), "test");

        bucket.UpdateVersioning(true);

        bucket.Versioning.Should().BeTrue();
    }

    [Fact]
    public void UpdateVersioning_WithFalse_DisablesVersioning()
    {
        StorageBucket bucket = StorageBucket.Create(TenantId.New(), "test", versioning: true);

        bucket.UpdateVersioning(false);

        bucket.Versioning.Should().BeFalse();
    }
}
