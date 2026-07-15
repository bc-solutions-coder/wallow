using Microsoft.Extensions.Time.Testing;
using Wallow.ApiKeys.Domain.ApiKeys;
using Wallow.ApiKeys.Domain.Entities;
using Wallow.Shared.Kernel.Domain;
using Wallow.Shared.Kernel.Identity;

namespace Wallow.ApiKeys.Tests.Domain;

public class ApiKeyTests
{
    private readonly TenantId _tenantId = TenantId.New();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly FakeTimeProvider _timeProvider = new();

    private ApiKey CreateValidApiKey(
        string serviceAccountId = "sa-client-1",
        string hashedKey = "sha256-hashed-key-value",
        string displayName = "Production Key",
        IEnumerable<string>? scopes = null,
        DateTimeOffset? expiresAt = null)
    {
        return ApiKey.Create(
            _tenantId,
            serviceAccountId,
            hashedKey,
            displayName,
            scopes ?? ["read", "write"],
            expiresAt,
            _userId,
            _timeProvider);
    }

    [Fact]
    public void Create_WithValidData_SetsAllProperties()
    {
        string[] scopes = ["read", "write", "admin"];
        DateTimeOffset expiry = _timeProvider.GetUtcNow().AddDays(30);

        ApiKey apiKey = ApiKey.Create(
            _tenantId,
            "sa-client-1",
            "hashed-key",
            "My Key",
            scopes,
            expiry,
            _userId,
            _timeProvider);

        apiKey.Id.Value.Should().NotBeEmpty();
        apiKey.TenantId.Should().Be(_tenantId);
        apiKey.ServiceAccountId.Should().Be("sa-client-1");
        apiKey.HashedKey.Should().Be("hashed-key");
        apiKey.DisplayName.Should().Be("My Key");
        apiKey.Scopes.Should().BeEquivalentTo(scopes);
        apiKey.ExpiresAt.Should().Be(expiry);
        apiKey.IsRevoked.Should().BeFalse();
    }

    [Fact]
    public void Create_WithNoExpiry_SetsExpiresAtToNull()
    {
        ApiKey apiKey = CreateValidApiKey(expiresAt: null);

        apiKey.ExpiresAt.Should().BeNull();
    }

    [Fact]
    public void Create_WithEmptyScopes_SetsEmptyScopesList()
    {
        ApiKey apiKey = CreateValidApiKey(scopes: []);

        apiKey.Scopes.Should().BeEmpty();
    }

    [Fact]
    public void Create_WithEmptyServiceAccountId_ThrowsBusinessRuleException()
    {
        Action act = () => CreateValidApiKey(serviceAccountId: "");

        act.Should().Throw<BusinessRuleException>()
            .Which.Code.Should().Be("ApiKeys.ServiceAccountIdRequired");
    }

    [Fact]
    public void Create_WithWhitespaceServiceAccountId_ThrowsBusinessRuleException()
    {
        Action act = () => CreateValidApiKey(serviceAccountId: "   ");

        act.Should().Throw<BusinessRuleException>()
            .Which.Code.Should().Be("ApiKeys.ServiceAccountIdRequired");
    }

    [Fact]
    public void Create_WithEmptyHashedKey_ThrowsBusinessRuleException()
    {
        Action act = () => CreateValidApiKey(hashedKey: "");

        act.Should().Throw<BusinessRuleException>()
            .Which.Code.Should().Be("ApiKeys.HashedKeyRequired");
    }

    [Fact]
    public void Create_WithWhitespaceHashedKey_ThrowsBusinessRuleException()
    {
        Action act = () => CreateValidApiKey(hashedKey: "   ");

        act.Should().Throw<BusinessRuleException>()
            .Which.Code.Should().Be("ApiKeys.HashedKeyRequired");
    }

    [Fact]
    public void Create_WithEmptyDisplayName_ThrowsBusinessRuleException()
    {
        Action act = () => CreateValidApiKey(displayName: "");

        act.Should().Throw<BusinessRuleException>()
            .Which.Code.Should().Be("ApiKeys.ApiKeyDisplayNameRequired");
    }

    [Fact]
    public void Create_WithWhitespaceDisplayName_ThrowsBusinessRuleException()
    {
        Action act = () => CreateValidApiKey(displayName: "   ");

        act.Should().Throw<BusinessRuleException>()
            .Which.Code.Should().Be("ApiKeys.ApiKeyDisplayNameRequired");
    }

    [Fact]
    public void Create_WithValidData_SetsAuditFields()
    {
        ApiKey apiKey = CreateValidApiKey();

        apiKey.CreatedAt.Should().Be(_timeProvider.GetUtcNow().UtcDateTime);
        apiKey.CreatedBy.Should().Be(_userId);
    }

    [Fact]
    public void Revoke_WhenNotRevoked_SetsIsRevokedToTrue()
    {
        ApiKey apiKey = CreateValidApiKey();

        apiKey.Revoke(_userId, _timeProvider);

        apiKey.IsRevoked.Should().BeTrue();
    }

    [Fact]
    public void Revoke_WhenNotRevoked_SetsUpdatedAuditFields()
    {
        ApiKey apiKey = CreateValidApiKey();
        _timeProvider.Advance(TimeSpan.FromHours(1));

        apiKey.Revoke(_userId, _timeProvider);

        apiKey.UpdatedAt.Should().Be(_timeProvider.GetUtcNow().UtcDateTime);
        apiKey.UpdatedBy.Should().Be(_userId);
    }

    [Fact]
    public void Revoke_WhenAlreadyRevoked_ThrowsBusinessRuleException()
    {
        ApiKey apiKey = CreateValidApiKey();
        apiKey.Revoke(_userId, _timeProvider);

        Action act = () => apiKey.Revoke(_userId, _timeProvider);

        act.Should().Throw<BusinessRuleException>()
            .Which.Code.Should().Be("ApiKeys.ApiKeyAlreadyRevoked");
    }

    [Fact]
    public void Create_GeneratesUniqueIds()
    {
        ApiKey first = CreateValidApiKey();
        ApiKey second = CreateValidApiKey();

        first.Id.Should().NotBe(second.Id);
    }
}

public class ApiKeyIdTests
{
    [Fact]
    public void New_GeneratesNonEmptyGuid()
    {
        ApiKeyId id = ApiKeyId.New();

        id.Value.Should().NotBeEmpty();
    }

    [Fact]
    public void New_GeneratesUniqueValues()
    {
        ApiKeyId first = ApiKeyId.New();
        ApiKeyId second = ApiKeyId.New();

        first.Should().NotBe(second);
    }

    [Fact]
    public void Create_WithGuid_PreservesValue()
    {
        Guid guid = Guid.NewGuid();

        ApiKeyId id = ApiKeyId.Create(guid);

        id.Value.Should().Be(guid);
    }

    [Fact]
    public void Equals_WithSameValue_ReturnsTrue()
    {
        Guid guid = Guid.NewGuid();
        ApiKeyId first = new(guid);
        ApiKeyId second = new(guid);

        first.Should().Be(second);
        (first == second).Should().BeTrue();
    }

    [Fact]
    public void Equals_WithDifferentValue_ReturnsFalse()
    {
        ApiKeyId first = ApiKeyId.New();
        ApiKeyId second = ApiKeyId.New();

        first.Should().NotBe(second);
        (first != second).Should().BeTrue();
    }

    [Fact]
    public void Constructor_WithGuid_SetsValue()
    {
        Guid guid = Guid.NewGuid();

        ApiKeyId id = new(guid);

        id.Value.Should().Be(guid);
    }
}
