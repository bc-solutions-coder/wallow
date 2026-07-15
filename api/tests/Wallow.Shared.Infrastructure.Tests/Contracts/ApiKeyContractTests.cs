using Wallow.Shared.Contracts.ApiKeys;

namespace Wallow.Shared.Infrastructure.Tests.Contracts;

public class ApiKeyContractTests
{
    // ── ApiKeyCreateResult ────────────────────────────────────────────────

    [Fact]
    public void ApiKeyCreateResult_Success_HasCorrectValues()
    {
        string keyId = Guid.NewGuid().ToString();
        string apiKey = "wlw_" + Guid.NewGuid().ToString("N");
        string prefix = apiKey[..8];

        ApiKeyCreateResult result = new(
            Success: true,
            KeyId: keyId,
            ApiKey: apiKey,
            Prefix: prefix,
            Error: null);

        result.Success.Should().BeTrue();
        result.KeyId.Should().Be(keyId);
        result.ApiKey.Should().Be(apiKey);
        result.Prefix.Should().Be(prefix);
        result.Error.Should().BeNull();
    }

    [Fact]
    public void ApiKeyCreateResult_Failure_HasCorrectValues()
    {
        ApiKeyCreateResult result = new(
            Success: false,
            KeyId: null,
            ApiKey: null,
            Prefix: null,
            Error: "API key limit exceeded");

        result.Success.Should().BeFalse();
        result.KeyId.Should().BeNull();
        result.ApiKey.Should().BeNull();
        result.Prefix.Should().BeNull();
        result.Error.Should().Be("API key limit exceeded");
    }

    // ── ApiKeyValidationResult ────────────────────────────────────────────

    [Fact]
    public void ApiKeyValidationResult_Valid_HasCorrectValues()
    {
        string keyId = Guid.NewGuid().ToString();
        Guid userId = Guid.NewGuid();
        Guid tenantId = Guid.NewGuid();
        List<string> scopes = new() { "read:invoices", "write:invoices" };

        ApiKeyValidationResult result = new(
            IsValid: true,
            KeyId: keyId,
            UserId: userId,
            TenantId: tenantId,
            Scopes: scopes,
            Error: null);

        result.IsValid.Should().BeTrue();
        result.KeyId.Should().Be(keyId);
        result.UserId.Should().Be(userId);
        result.TenantId.Should().Be(tenantId);
        result.Scopes.Should().HaveCount(2);
        result.Error.Should().BeNull();
    }

    [Fact]
    public void ApiKeyValidationResult_Invalid_HasCorrectValues()
    {
        ApiKeyValidationResult result = new(
            IsValid: false,
            KeyId: null,
            UserId: null,
            TenantId: null,
            Scopes: null,
            Error: "Key expired");

        result.IsValid.Should().BeFalse();
        result.KeyId.Should().BeNull();
        result.UserId.Should().BeNull();
        result.TenantId.Should().BeNull();
        result.Scopes.Should().BeNull();
        result.Error.Should().Be("Key expired");
    }

    [Fact]
    public void ApiKeyValidationResult_ValidWithNullScopes_HasCorrectValues()
    {
        Guid userId = Guid.NewGuid();
        Guid tenantId = Guid.NewGuid();

        ApiKeyValidationResult result = new(
            IsValid: true,
            KeyId: Guid.NewGuid().ToString(),
            UserId: userId,
            TenantId: tenantId,
            Scopes: null,
            Error: null);

        result.IsValid.Should().BeTrue();
        result.UserId.Should().Be(userId);
        result.TenantId.Should().Be(tenantId);
        result.Scopes.Should().BeNull();
    }

    [Fact]
    public void ApiKeyValidationResult_ValidWithScopes_HasCorrectValues()
    {
        List<string> scopes = new() { "admin", "billing:read" };

        ApiKeyValidationResult result = new(
            IsValid: true,
            KeyId: Guid.NewGuid().ToString(),
            UserId: Guid.NewGuid(),
            TenantId: Guid.NewGuid(),
            Scopes: scopes,
            Error: null);

        result.Scopes.Should().NotBeNull();
        result.Scopes.Should().HaveCount(2);
        result.Scopes![0].Should().Be("admin");
        result.Scopes[1].Should().Be("billing:read");
    }

    // ── ApiKeyMetadata ────────────────────────────────────────────────────

    [Fact]
    public void ApiKeyMetadata_WithAllFields_HasCorrectValues()
    {
        string keyId = Guid.NewGuid().ToString();
        Guid userId = Guid.NewGuid();
        Guid tenantId = Guid.NewGuid();
        List<string> scopes = new() { "read", "write" };
        DateTimeOffset createdAt = DateTimeOffset.UtcNow;
        DateTimeOffset expiresAt = DateTimeOffset.UtcNow.AddDays(90);
        DateTimeOffset lastUsedAt = DateTimeOffset.UtcNow.AddHours(-1);

        ApiKeyMetadata metadata = new(
            KeyId: keyId,
            Name: "Production Key",
            Prefix: "wlw_abc1",
            UserId: userId,
            TenantId: tenantId,
            Scopes: scopes,
            CreatedAt: createdAt,
            ExpiresAt: expiresAt,
            LastUsedAt: lastUsedAt);

        metadata.KeyId.Should().Be(keyId);
        metadata.Name.Should().Be("Production Key");
        metadata.Prefix.Should().Be("wlw_abc1");
        metadata.UserId.Should().Be(userId);
        metadata.TenantId.Should().Be(tenantId);
        metadata.Scopes.Should().HaveCount(2);
        metadata.CreatedAt.Should().Be(createdAt);
        metadata.ExpiresAt.Should().Be(expiresAt);
        metadata.LastUsedAt.Should().Be(lastUsedAt);
    }

    [Fact]
    public void ApiKeyMetadata_WithNullOptionalFields_HasCorrectValues()
    {
        Guid userId = Guid.NewGuid();
        Guid tenantId = Guid.NewGuid();
        DateTimeOffset createdAt = DateTimeOffset.UtcNow;
        List<string> scopes = new() { "read" };

        ApiKeyMetadata metadata = new(
            KeyId: Guid.NewGuid().ToString(),
            Name: "Dev Key",
            Prefix: "wlw_dev1",
            UserId: userId,
            TenantId: tenantId,
            Scopes: scopes,
            CreatedAt: createdAt,
            ExpiresAt: null,
            LastUsedAt: null);

        metadata.Name.Should().Be("Dev Key");
        metadata.UserId.Should().Be(userId);
        metadata.TenantId.Should().Be(tenantId);
        metadata.Scopes.Should().HaveCount(1);
        metadata.CreatedAt.Should().Be(createdAt);
        metadata.ExpiresAt.Should().BeNull();
        metadata.LastUsedAt.Should().BeNull();
    }
}
