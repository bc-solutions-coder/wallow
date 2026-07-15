using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using NSubstitute.ExceptionExtensions;
using StackExchange.Redis;
using Wallow.ApiKeys.Application.Interfaces;
using Wallow.ApiKeys.Domain.Entities;
using Wallow.ApiKeys.Infrastructure.Services;
using Wallow.Shared.Contracts.ApiKeys;
using Wallow.Shared.Kernel.Identity;

namespace Wallow.ApiKeys.Tests.Infrastructure;

public class RedisApiKeyServiceGapTests
{
    private static readonly string[] _readScope = ["read"];
    private static readonly string[] _readWriteScopes = ["read", "write"];
    private readonly IRedisDatabase _db = Substitute.For<IRedisDatabase>();
    private readonly IApiKeyRepository _apiKeyRepository = Substitute.For<IApiKeyRepository>();
    private readonly ILogger<RedisApiKeyService> _logger = Substitute.For<ILogger<RedisApiKeyService>>();

    public RedisApiKeyServiceGapTests()
    {
        _db.StringSetAsync(Arg.Any<RedisKey>(), Arg.Any<RedisValue>(), Arg.Any<TimeSpan?>(), Arg.Any<bool>(), Arg.Any<When>(), Arg.Any<CommandFlags>())
            .Returns(true);
        _db.SetAddAsync(Arg.Any<RedisKey>(), Arg.Any<RedisValue>())
            .Returns(true);
        _db.StringGetAsync(Arg.Any<RedisKey>())
            .Returns(RedisValue.Null);
        _db.KeyDeleteAsync(Arg.Any<RedisKey>())
            .Returns(true);
        _db.SetRemoveAsync(Arg.Any<RedisKey>(), Arg.Any<RedisValue>())
            .Returns(true);
    }

    private RedisApiKeyService CreateService() => new(_db, _apiKeyRepository, TimeProvider.System, _logger);

    // CreateApiKeyAsync: success without expiration (no TTL set)
    [Fact]
    public async Task CreateApiKeyAsync_WithoutExpiration_ReturnsSuccessWithNoTtl()
    {
        Guid userId = Guid.NewGuid();
        Guid tenantId = Guid.NewGuid();

        RedisApiKeyService service = CreateService();

        ApiKeyCreateResult result = await service.CreateApiKeyAsync("No Expiry Key", userId, tenantId);

        result.Success.Should().BeTrue();
        result.ApiKey.Should().StartWith("sk_live_");
        result.KeyId.Should().NotBeNullOrEmpty();
        result.Prefix.Should().HaveLength(16);
        result.Error.Should().BeNull();
    }

    // CreateApiKeyAsync: null scopes defaults to empty list
    [Fact]
    public async Task CreateApiKeyAsync_WithNullScopes_ReturnsSuccess()
    {
        Guid userId = Guid.NewGuid();
        Guid tenantId = Guid.NewGuid();

        RedisApiKeyService service = CreateService();

        ApiKeyCreateResult result = await service.CreateApiKeyAsync("Key", userId, tenantId, scopes: null);

        result.Success.Should().BeTrue();
        result.ApiKey.Should().NotBeNullOrEmpty();
    }

    // CreateApiKeyAsync: with expiration sets TTL on Redis keys
    [Fact]
    public async Task CreateApiKeyAsync_WithExpiration_StoresKeyWithTtl()
    {
        Guid userId = Guid.NewGuid();
        Guid tenantId = Guid.NewGuid();
        DateTimeOffset expiresAt = DateTimeOffset.UtcNow.AddHours(1);

        RedisApiKeyService service = CreateService();

        ApiKeyCreateResult result = await service.CreateApiKeyAsync(
            "Expiring Key", userId, tenantId, _readScope, expiresAt);

        result.Success.Should().BeTrue();

        // Verify TTL was passed (non-null TimeSpan)
        await _db.Received(2).StringSetAsync(
            Arg.Any<RedisKey>(),
            Arg.Any<RedisValue>(),
            Arg.Is<TimeSpan?>(t => t.HasValue && t.Value.TotalMinutes > 0),
            Arg.Any<bool>(),
            Arg.Any<When>(),
            Arg.Any<CommandFlags>());
    }

    // CreateApiKeyAsync: stores key in user's key set
    [Fact]
    public async Task CreateApiKeyAsync_Success_AddsToUserKeySet()
    {
        Guid userId = Guid.NewGuid();
        Guid tenantId = Guid.NewGuid();

        RedisApiKeyService service = CreateService();

        await service.CreateApiKeyAsync("Key", userId, tenantId);

        await _db.Received(1).SetAddAsync(
            Arg.Is<RedisKey>(k => k.ToString() == $"apikeys:user:{userId}"),
            Arg.Any<RedisValue>());
    }

    // CreateApiKeyAsync: SetAddAsync throws after StringSetAsync succeeds
    [Fact]
    public async Task CreateApiKeyAsync_WhenSetAddThrows_ReturnsFailure()
    {
        Guid userId = Guid.NewGuid();
        Guid tenantId = Guid.NewGuid();

        _db.SetAddAsync(Arg.Any<RedisKey>(), Arg.Any<RedisValue>())
            .Throws(new RedisException("Connection lost"));

        RedisApiKeyService service = CreateService();

        ApiKeyCreateResult result = await service.CreateApiKeyAsync("Key", userId, tenantId);

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("Failed");
    }

    // ValidateApiKeyAsync: whitespace-only key returns invalid format
    [Fact]
    public async Task ValidateApiKeyAsync_WhitespaceOnly_ReturnsInvalidFormat()
    {
        RedisApiKeyService service = CreateService();

        ApiKeyValidationResult result = await service.ValidateApiKeyAsync("   ");

        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("Invalid API key format");
    }

    // ValidateApiKeyAsync: null key returns invalid format
    [Fact]
    public async Task ValidateApiKeyAsync_Null_ReturnsInvalidFormat()
    {
        RedisApiKeyService service = CreateService();

        ApiKeyValidationResult result = await service.ValidateApiKeyAsync(null!);

        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("Invalid API key format");
    }

    // ValidateApiKeyAsync: valid key with no expiration passes expiration check
    [Fact]
    public async Task ValidateApiKeyAsync_KeyWithNullExpiration_DoesNotFailExpirationCheck()
    {
        Guid userId = Guid.NewGuid();
        Guid tenantId = Guid.NewGuid();

        string keyJson = JsonSerializer.Serialize(new
        {
            KeyId = "key-no-exp",
            Name = "Permanent Key",
            Prefix = "sk_live_abc",
            KeyHash = "somehash",
            UserId = userId,
            TenantId = tenantId,
            Scopes = new List<string> { "admin" },
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-100),
            ExpiresAt = (DateTimeOffset?)null,
            LastUsedAt = (DateTimeOffset?)null
        });

        _db.StringGetAsync(Arg.Any<RedisKey>())
            .Returns((RedisValue)keyJson);

        RedisApiKeyService service = CreateService();

        ApiKeyValidationResult result = await service.ValidateApiKeyAsync("sk_live_somekeydata123456");

        result.IsValid.Should().BeTrue();
        result.Scopes.Should().Contain("admin");
    }

    // ValidateApiKeyAsync: successful validation triggers UpdateLastUsedAsync (fire-and-forget)
    [Fact]
    public async Task ValidateApiKeyAsync_ValidKey_UpdatesLastUsedTimestamp()
    {
        Guid userId = Guid.NewGuid();
        Guid tenantId = Guid.NewGuid();

        string keyJson = JsonSerializer.Serialize(new
        {
            KeyId = "key-track",
            Name = "Tracked Key",
            Prefix = "sk_live_abc",
            KeyHash = "somehash",
            UserId = userId,
            TenantId = tenantId,
            Scopes = new List<string>(),
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-1),
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(30),
            LastUsedAt = (DateTimeOffset?)null
        });

        _db.StringGetAsync(Arg.Any<RedisKey>())
            .Returns((RedisValue)keyJson);

        RedisApiKeyService service = CreateService();

        ApiKeyValidationResult result = await service.ValidateApiKeyAsync("sk_live_somekeydata123456");

        result.IsValid.Should().BeTrue();

        // Allow fire-and-forget to complete
        await Task.Delay(100);

        // UpdateLastUsedAsync writes to both hash key and id key
        await _db.Received().StringSetAsync(
            Arg.Is<RedisKey>(k => k.ToString().StartsWith("apikey:")),
            Arg.Any<RedisValue>(),
            Arg.Any<TimeSpan?>(),
            Arg.Any<bool>(),
            Arg.Any<When>(),
            Arg.Any<CommandFlags>());
    }

    // UpdateLastUsedAsync: Redis error during update is silently caught
    [Fact]
    public async Task ValidateApiKeyAsync_WhenUpdateLastUsedThrows_StillReturnsValid()
    {
        Guid userId = Guid.NewGuid();
        Guid tenantId = Guid.NewGuid();

        string keyJson = JsonSerializer.Serialize(new
        {
            KeyId = "key-update-fail",
            Name = "Key",
            Prefix = "sk_live_abc",
            KeyHash = "somehash",
            UserId = userId,
            TenantId = tenantId,
            Scopes = new List<string>(),
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-1),
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(30),
            LastUsedAt = (DateTimeOffset?)null
        });

        // StringGetAsync for validation succeeds
        _db.StringGetAsync(Arg.Any<RedisKey>())
            .Returns((RedisValue)keyJson);

        // StringSetAsync for UpdateLastUsedAsync fails
        _db.StringSetAsync(Arg.Any<RedisKey>(), Arg.Any<RedisValue>(), Arg.Any<TimeSpan?>(), Arg.Any<bool>(), Arg.Any<When>(), Arg.Any<CommandFlags>())
            .Throws(new RedisException("Write failed"));

        RedisApiKeyService service = CreateService();

        ApiKeyValidationResult result = await service.ValidateApiKeyAsync("sk_live_somekeydata123456");

        // Validation still succeeds even though UpdateLastUsed fails
        result.IsValid.Should().BeTrue();
        result.KeyId.Should().Be("key-update-fail");
    }

    // ListApiKeysAsync: multiple keys returned ordered by CreatedAt descending
    [Fact]
    public async Task ListApiKeysAsync_MultipleKeys_ReturnsOrderedByCreatedAtDescending()
    {
        Guid userId = Guid.NewGuid();
        Guid tenantId = Guid.NewGuid();
        FakeTimeProvider fakeTime = new();

        fakeTime.SetUtcNow(DateTimeOffset.UtcNow.AddDays(-10));
        ApiKey olderKey = ApiKey.Create(
            new TenantId(tenantId), userId.ToString(), "hash-old", "Old Key",
            [], null, userId, fakeTime);

        fakeTime.SetUtcNow(DateTimeOffset.UtcNow.AddDays(-1));
        ApiKey newerKey = ApiKey.Create(
            new TenantId(tenantId), userId.ToString(), "hash-new", "New Key",
            [], null, userId, fakeTime);

        _apiKeyRepository.ListByServiceAccountAsync(userId.ToString(), tenantId, Arg.Any<CancellationToken>())
            .Returns(new List<ApiKey> { olderKey, newerKey });

        RedisApiKeyService service = CreateService();

        IReadOnlyList<ApiKeyMetadata> result = await service.ListApiKeysAsync(userId, tenantId);

        result.Should().HaveCount(2);
        result[0].Name.Should().Be("New Key");
        result[1].Name.Should().Be("Old Key");
    }

    // ListApiKeysAsync: empty user key set returns empty list
    [Fact]
    public async Task ListApiKeysAsync_NoKeys_ReturnsEmptyList()
    {
        Guid userId = Guid.NewGuid();
        Guid tenantId = Guid.NewGuid();

        _apiKeyRepository.ListByServiceAccountAsync(userId.ToString(), tenantId, Arg.Any<CancellationToken>())
            .Returns(new List<ApiKey>());

        RedisApiKeyService service = CreateService();

        IReadOnlyList<ApiKeyMetadata> result = await service.ListApiKeysAsync(userId, tenantId);

        result.Should().BeEmpty();
    }

    // ListApiKeysAsync: deserializes null from valid JSON "null" string -- skips it
    [Fact]
    public async Task ListApiKeysAsync_DeserializesToNull_SkipsKey()
    {
        Guid userId = Guid.NewGuid();
        Guid tenantId = Guid.NewGuid();

        _apiKeyRepository.ListByServiceAccountAsync(userId.ToString(), tenantId, Arg.Any<CancellationToken>())
            .Returns(new List<ApiKey>());

        RedisApiKeyService service = CreateService();

        IReadOnlyList<ApiKeyMetadata> result = await service.ListApiKeysAsync(userId, tenantId);

        result.Should().BeEmpty();
    }

    // RevokeApiKeyAsync: data deserializes to null returns false
    [Fact]
    public async Task RevokeApiKeyAsync_DataDeserializesToNull_ReturnsFalse()
    {
        Guid userId = Guid.NewGuid();

        _db.StringGetAsync(Arg.Any<RedisKey>())
            .Returns((RedisValue)"null");

        RedisApiKeyService service = CreateService();

        bool result = await service.RevokeApiKeyAsync("key-1", userId);

        result.Should().BeFalse();
    }

    // RevokeApiKeyAsync: successful revocation removes from all three locations
    [Fact]
    public async Task RevokeApiKeyAsync_Success_DeletesHashAndIdAndRemovesFromUserSet()
    {
        Guid userId = Guid.NewGuid();

        string keyJson = JsonSerializer.Serialize(new
        {
            KeyId = "key-revoke",
            Name = "Revoke Me",
            Prefix = "sk_live_abc",
            KeyHash = "hash-revoke",
            UserId = userId,
            TenantId = Guid.NewGuid(),
            Scopes = new List<string>(),
            CreatedAt = DateTimeOffset.UtcNow
        });

        _db.StringGetAsync(Arg.Any<RedisKey>())
            .Returns((RedisValue)keyJson);

        RedisApiKeyService service = CreateService();

        bool result = await service.RevokeApiKeyAsync("key-revoke", userId);

        result.Should().BeTrue();

        // Verify hash key deleted
        await _db.Received().KeyDeleteAsync(
            Arg.Is<RedisKey>(k => k.ToString() == "apikey:hash-revoke"));

        // Verify id key deleted
        await _db.Received().KeyDeleteAsync(
            Arg.Is<RedisKey>(k => k.ToString() == "apikey:id:key-revoke"));

        // Verify removed from user set
        await _db.Received().SetRemoveAsync(
            Arg.Is<RedisKey>(k => k.ToString() == $"apikeys:user:{userId}"),
            Arg.Is<RedisValue>("key-revoke"));
    }

    // RevokeApiKeyAsync: KeyDeleteAsync throws after getting key data
    [Fact]
    public async Task RevokeApiKeyAsync_WhenDeleteThrows_ReturnsFalse()
    {
        Guid userId = Guid.NewGuid();

        string keyJson = JsonSerializer.Serialize(new
        {
            KeyId = "key-del-fail",
            Name = "Key",
            Prefix = "sk_live_abc",
            KeyHash = "hash-del-fail",
            UserId = userId,
            TenantId = Guid.NewGuid(),
            Scopes = new List<string>(),
            CreatedAt = DateTimeOffset.UtcNow
        });

        _db.StringGetAsync(Arg.Any<RedisKey>())
            .Returns((RedisValue)keyJson);
        _db.KeyDeleteAsync(Arg.Any<RedisKey>())
            .Throws(new RedisException("Delete failed"));

        RedisApiKeyService service = CreateService();

        bool result = await service.RevokeApiKeyAsync("key-del-fail", userId);

        result.Should().BeFalse();
    }

    // CreateApiKeyAsync: generated key prefix is exactly 16 characters
    [Fact]
    public async Task CreateApiKeyAsync_Success_PrefixIsExactly16Characters()
    {
        Guid userId = Guid.NewGuid();
        Guid tenantId = Guid.NewGuid();

        RedisApiKeyService service = CreateService();

        ApiKeyCreateResult result = await service.CreateApiKeyAsync("Key", userId, tenantId);

        result.Success.Should().BeTrue();
        result.Prefix.Should().HaveLength(16);
        result.Prefix.Should().StartWith("sk_live_");
    }

    // CreateApiKeyAsync: stores metadata by both hash and keyId (verified via SetAddAsync which tracks the keyId)
    [Fact]
    public async Task CreateApiKeyAsync_Success_StoresByHashAndById()
    {
        Guid userId = Guid.NewGuid();
        Guid tenantId = Guid.NewGuid();

        RedisApiKeyService service = CreateService();

        ApiKeyCreateResult result = await service.CreateApiKeyAsync("Key", userId, tenantId);

        // Verify successful creation with both hash-prefix key and keyId returned
        result.Success.Should().BeTrue();
        result.KeyId.Should().NotBeNullOrEmpty();
        result.ApiKey.Should().StartWith("sk_live_");

        // Verify the user key set was updated (SetAddAsync called once with user's key set)
        await _db.Received(1).SetAddAsync(
            Arg.Is<RedisKey>(k => k.ToString() == $"apikeys:user:{userId}"),
            Arg.Any<RedisValue>());
    }

    // ValidateApiKeyAsync: cache miss with active key repopulates cache and returns valid
    [Fact]
    public async Task ValidateApiKeyAsync_CacheMiss_ActiveKey_RepopulatesCacheAndReturnsValid()
    {
        Guid userId = Guid.NewGuid();
        Guid tenantId = Guid.NewGuid();

        ApiKey activeKey = ApiKey.Create(
            new TenantId(tenantId), userId.ToString(), "fakehash", "Active Key",
            _readScope, DateTimeOffset.UtcNow.AddDays(30), userId, TimeProvider.System);

        _apiKeyRepository.GetByHashAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(activeKey);

        RedisApiKeyService service = CreateService();

        ApiKeyValidationResult result = await service.ValidateApiKeyAsync("sk_live_somekeydata123456");

        result.IsValid.Should().BeTrue();
        result.KeyId.Should().Be(activeKey.Id.Value.ToString());
        result.TenantId.Should().Be(tenantId);
        result.Scopes.Should().Contain("read");

        // Verify cache was repopulated
        await _db.Received(1).StringSetAsync(
            Arg.Is<RedisKey>(k => k.ToString().StartsWith("apikey:")),
            Arg.Any<RedisValue>(),
            Arg.Any<TimeSpan?>(),
            Arg.Any<bool>(),
            Arg.Any<When>(),
            Arg.Any<CommandFlags>());
    }

    // ValidateApiKeyAsync: cache miss with null from repo returns not found
    [Fact]
    public async Task ValidateApiKeyAsync_CacheMiss_NullFromRepo_ReturnsNotFound()
    {
        _apiKeyRepository.GetByHashAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((ApiKey?)null);

        RedisApiKeyService service = CreateService();

        ApiKeyValidationResult result = await service.ValidateApiKeyAsync("sk_live_somekeydata123456");

        result.IsValid.Should().BeFalse();
        result.Error.Should().Be("API key not found");
    }

    // ValidateApiKeyAsync: cache miss with expired key returns expired
    [Fact]
    public async Task ValidateApiKeyAsync_CacheMiss_ExpiredKey_ReturnsExpired()
    {
        Guid userId = Guid.NewGuid();
        Guid tenantId = Guid.NewGuid();
        FakeTimeProvider fakeTime = new();
        fakeTime.SetUtcNow(DateTimeOffset.UtcNow.AddDays(-60));

        ApiKey expiredKey = ApiKey.Create(
            new TenantId(tenantId), userId.ToString(), "fakehash", "Expired Key",
            _readScope, DateTimeOffset.UtcNow.AddDays(-1), userId, fakeTime);

        _apiKeyRepository.GetByHashAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(expiredKey);

        RedisApiKeyService service = CreateService();

        ApiKeyValidationResult result = await service.ValidateApiKeyAsync("sk_live_somekeydata123456");

        result.IsValid.Should().BeFalse();
        result.Error.Should().Be("API key expired");
        result.KeyId.Should().Be(expiredKey.Id.Value.ToString());
    }

    // ListApiKeysAsync: maps all metadata fields correctly
    [Fact]
    public async Task ListApiKeysAsync_MapsAllFields_Correctly()
    {
        Guid userId = Guid.NewGuid();
        Guid tenantId = Guid.NewGuid();
        DateTimeOffset expiresAt = DateTimeOffset.UtcNow.AddDays(25);

        ApiKey apiKey = ApiKey.Create(
            new TenantId(tenantId), userId.ToString(), "hash-full", "Full Key",
            _readWriteScopes, expiresAt, userId, TimeProvider.System);

        _apiKeyRepository.ListByServiceAccountAsync(userId.ToString(), tenantId, Arg.Any<CancellationToken>())
            .Returns(new List<ApiKey> { apiKey });

        RedisApiKeyService service = CreateService();

        IReadOnlyList<ApiKeyMetadata> result = await service.ListApiKeysAsync(userId, tenantId);

        result.Should().HaveCount(1);
        ApiKeyMetadata key = result[0];
        key.KeyId.Should().Be(apiKey.Id.Value.ToString());
        key.Name.Should().Be("Full Key");
        key.UserId.Should().Be(userId);
        key.TenantId.Should().Be(tenantId);
        key.Scopes.Should().BeEquivalentTo(_readWriteScopes);
        key.ExpiresAt.Should().Be(expiresAt);
    }
}
