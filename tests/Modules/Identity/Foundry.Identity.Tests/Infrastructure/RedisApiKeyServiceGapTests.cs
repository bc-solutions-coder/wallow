using System.Text.Json;
using Foundry.Identity.Application.Interfaces;
using Foundry.Identity.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using NSubstitute.ExceptionExtensions;
using StackExchange.Redis;

namespace Foundry.Identity.Tests.Infrastructure;

public class RedisApiKeyServiceGapTests
{
    private static readonly string[] _readScope = ["read"];
    private static readonly string[] _readWriteScopes = ["read", "write"];
    private readonly IConnectionMultiplexer _redis = Substitute.For<IConnectionMultiplexer>();
    private readonly IDatabase _db = Substitute.For<IDatabase>();
    private readonly ILogger<RedisApiKeyService> _logger = Substitute.For<ILogger<RedisApiKeyService>>();

    public RedisApiKeyServiceGapTests()
    {
        _redis.GetDatabase(Arg.Any<int>(), Arg.Any<object?>()).Returns(_db);
        _redis.GetDatabase().Returns(_db);
    }

    // CreateApiKeyAsync: success without expiration (no TTL set)
    [Fact]
    public async Task CreateApiKeyAsync_WithoutExpiration_ReturnsSuccessWithNoTtl()
    {
        Guid userId = Guid.NewGuid();
        Guid tenantId = Guid.NewGuid();

        _db.StringSetAsync(Arg.Any<RedisKey>(), Arg.Any<RedisValue>(), Arg.Any<TimeSpan?>(),
                Arg.Any<bool>(), Arg.Any<When>(), Arg.Any<CommandFlags>())
            .Returns(true);
        _db.SetAddAsync(Arg.Any<RedisKey>(), Arg.Any<RedisValue>(), Arg.Any<CommandFlags>())
            .Returns(true);

        RedisApiKeyService service = new(_redis, _logger);

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

        _db.StringSetAsync(Arg.Any<RedisKey>(), Arg.Any<RedisValue>(), Arg.Any<TimeSpan?>(),
                Arg.Any<bool>(), Arg.Any<When>(), Arg.Any<CommandFlags>())
            .Returns(true);
        _db.SetAddAsync(Arg.Any<RedisKey>(), Arg.Any<RedisValue>(), Arg.Any<CommandFlags>())
            .Returns(true);

        RedisApiKeyService service = new(_redis, _logger);

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

        _db.StringSetAsync(Arg.Any<RedisKey>(), Arg.Any<RedisValue>(), Arg.Any<TimeSpan?>(),
                Arg.Any<bool>(), Arg.Any<When>(), Arg.Any<CommandFlags>())
            .Returns(true);
        _db.SetAddAsync(Arg.Any<RedisKey>(), Arg.Any<RedisValue>(), Arg.Any<CommandFlags>())
            .Returns(true);

        RedisApiKeyService service = new(_redis, _logger);

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

        _db.StringSetAsync(Arg.Any<RedisKey>(), Arg.Any<RedisValue>(), Arg.Any<TimeSpan?>(),
                Arg.Any<bool>(), Arg.Any<When>(), Arg.Any<CommandFlags>())
            .Returns(true);
        _db.SetAddAsync(Arg.Any<RedisKey>(), Arg.Any<RedisValue>(), Arg.Any<CommandFlags>())
            .Returns(true);

        RedisApiKeyService service = new(_redis, _logger);

        await service.CreateApiKeyAsync("Key", userId, tenantId);

        await _db.Received(1).SetAddAsync(
            Arg.Is<RedisKey>(k => k.ToString() == $"apikeys:user:{userId}"),
            Arg.Any<RedisValue>(),
            Arg.Any<CommandFlags>());
    }

    // CreateApiKeyAsync: SetAddAsync throws after StringSetAsync succeeds
    [Fact]
    public async Task CreateApiKeyAsync_WhenSetAddThrows_ReturnsFailure()
    {
        Guid userId = Guid.NewGuid();
        Guid tenantId = Guid.NewGuid();

        _db.StringSetAsync(Arg.Any<RedisKey>(), Arg.Any<RedisValue>(), Arg.Any<TimeSpan?>(),
                Arg.Any<bool>(), Arg.Any<When>(), Arg.Any<CommandFlags>())
            .Returns(true);
        _db.SetAddAsync(Arg.Any<RedisKey>(), Arg.Any<RedisValue>(), Arg.Any<CommandFlags>())
            .Throws(new RedisException("Connection lost"));

        RedisApiKeyService service = new(_redis, _logger);

        ApiKeyCreateResult result = await service.CreateApiKeyAsync("Key", userId, tenantId);

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("Failed");
    }

    // ValidateApiKeyAsync: whitespace-only key returns invalid format
    [Fact]
    public async Task ValidateApiKeyAsync_WhitespaceOnly_ReturnsInvalidFormat()
    {
        RedisApiKeyService service = new(_redis, _logger);

        ApiKeyValidationResult result = await service.ValidateApiKeyAsync("   ");

        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("Invalid API key format");
    }

    // ValidateApiKeyAsync: null key returns invalid format
    [Fact]
    public async Task ValidateApiKeyAsync_Null_ReturnsInvalidFormat()
    {
        RedisApiKeyService service = new(_redis, _logger);

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

        _db.StringGetAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>())
            .Returns((RedisValue)keyJson);
        _db.StringSetAsync(Arg.Any<RedisKey>(), Arg.Any<RedisValue>(), Arg.Any<TimeSpan?>(),
                Arg.Any<bool>(), Arg.Any<When>(), Arg.Any<CommandFlags>())
            .Returns(true);

        RedisApiKeyService service = new(_redis, _logger);

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

        _db.StringGetAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>())
            .Returns((RedisValue)keyJson);
        _db.StringSetAsync(Arg.Any<RedisKey>(), Arg.Any<RedisValue>(), Arg.Any<TimeSpan?>(),
                Arg.Any<bool>(), Arg.Any<When>(), Arg.Any<CommandFlags>())
            .Returns(true);

        RedisApiKeyService service = new(_redis, _logger);

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
        _db.StringGetAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>())
            .Returns((RedisValue)keyJson);

        // StringSetAsync for UpdateLastUsedAsync fails
        _db.StringSetAsync(Arg.Any<RedisKey>(), Arg.Any<RedisValue>(), Arg.Any<TimeSpan?>(),
                Arg.Any<bool>(), Arg.Any<When>(), Arg.Any<CommandFlags>())
            .Throws(new RedisException("Write failed"));

        RedisApiKeyService service = new(_redis, _logger);

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

        DateTimeOffset older = DateTimeOffset.UtcNow.AddDays(-10);
        DateTimeOffset newer = DateTimeOffset.UtcNow.AddDays(-1);

        string olderKeyJson = JsonSerializer.Serialize(new
        {
            KeyId = "key-old",
            Name = "Old Key",
            Prefix = "sk_live_old",
            KeyHash = "hash-old",
            UserId = userId,
            TenantId = tenantId,
            Scopes = new List<string>(),
            CreatedAt = older,
            ExpiresAt = (DateTimeOffset?)null,
            LastUsedAt = (DateTimeOffset?)null
        });

        string newerKeyJson = JsonSerializer.Serialize(new
        {
            KeyId = "key-new",
            Name = "New Key",
            Prefix = "sk_live_new",
            KeyHash = "hash-new",
            UserId = userId,
            TenantId = tenantId,
            Scopes = new List<string>(),
            CreatedAt = newer,
            ExpiresAt = (DateTimeOffset?)null,
            LastUsedAt = (DateTimeOffset?)null
        });

        _db.SetMembersAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>())
            .Returns(new RedisValue[] { "key-old", "key-new" });

        _db.StringGetAsync(Arg.Is<RedisKey>(k => k.ToString().Contains("id:key-old")), Arg.Any<CommandFlags>())
            .Returns((RedisValue)olderKeyJson);
        _db.StringGetAsync(Arg.Is<RedisKey>(k => k.ToString().Contains("id:key-new")), Arg.Any<CommandFlags>())
            .Returns((RedisValue)newerKeyJson);

        RedisApiKeyService service = new(_redis, _logger);

        IReadOnlyList<ApiKeyMetadata> result = await service.ListApiKeysAsync(userId);

        result.Should().HaveCount(2);
        result[0].KeyId.Should().Be("key-new");
        result[1].KeyId.Should().Be("key-old");
    }

    // ListApiKeysAsync: empty user key set returns empty list
    [Fact]
    public async Task ListApiKeysAsync_NoKeys_ReturnsEmptyList()
    {
        Guid userId = Guid.NewGuid();

        _db.SetMembersAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>())
            .Returns(Array.Empty<RedisValue>());

        RedisApiKeyService service = new(_redis, _logger);

        IReadOnlyList<ApiKeyMetadata> result = await service.ListApiKeysAsync(userId);

        result.Should().BeEmpty();
    }

    // ListApiKeysAsync: deserializes null from valid JSON "null" string — skips it
    [Fact]
    public async Task ListApiKeysAsync_DeserializesToNull_SkipsKey()
    {
        Guid userId = Guid.NewGuid();

        _db.SetMembersAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>())
            .Returns(new RedisValue[] { "key-null" });

        _db.StringGetAsync(Arg.Is<RedisKey>(k => k.ToString().Contains("id:key-null")), Arg.Any<CommandFlags>())
            .Returns((RedisValue)"null");

        RedisApiKeyService service = new(_redis, _logger);

        IReadOnlyList<ApiKeyMetadata> result = await service.ListApiKeysAsync(userId);

        result.Should().BeEmpty();
    }

    // RevokeApiKeyAsync: data deserializes to null returns false
    [Fact]
    public async Task RevokeApiKeyAsync_DataDeserializesToNull_ReturnsFalse()
    {
        Guid userId = Guid.NewGuid();

        _db.StringGetAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>())
            .Returns((RedisValue)"null");

        RedisApiKeyService service = new(_redis, _logger);

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

        _db.StringGetAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>())
            .Returns((RedisValue)keyJson);

        RedisApiKeyService service = new(_redis, _logger);

        bool result = await service.RevokeApiKeyAsync("key-revoke", userId);

        result.Should().BeTrue();

        // Verify hash key deleted
        await _db.Received().KeyDeleteAsync(
            Arg.Is<RedisKey>(k => k.ToString() == "apikey:hash-revoke"),
            Arg.Any<CommandFlags>());

        // Verify id key deleted
        await _db.Received().KeyDeleteAsync(
            Arg.Is<RedisKey>(k => k.ToString() == "apikey:id:key-revoke"),
            Arg.Any<CommandFlags>());

        // Verify removed from user set
        await _db.Received().SetRemoveAsync(
            Arg.Is<RedisKey>(k => k.ToString() == $"apikeys:user:{userId}"),
            Arg.Is<RedisValue>("key-revoke"),
            Arg.Any<CommandFlags>());
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

        _db.StringGetAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>())
            .Returns((RedisValue)keyJson);
        _db.KeyDeleteAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>())
            .Throws(new RedisException("Delete failed"));

        RedisApiKeyService service = new(_redis, _logger);

        bool result = await service.RevokeApiKeyAsync("key-del-fail", userId);

        result.Should().BeFalse();
    }

    // CreateApiKeyAsync: generated key prefix is exactly 16 characters
    [Fact]
    public async Task CreateApiKeyAsync_Success_PrefixIsExactly16Characters()
    {
        Guid userId = Guid.NewGuid();
        Guid tenantId = Guid.NewGuid();

        _db.StringSetAsync(Arg.Any<RedisKey>(), Arg.Any<RedisValue>(), Arg.Any<TimeSpan?>(),
                Arg.Any<bool>(), Arg.Any<When>(), Arg.Any<CommandFlags>())
            .Returns(true);
        _db.SetAddAsync(Arg.Any<RedisKey>(), Arg.Any<RedisValue>(), Arg.Any<CommandFlags>())
            .Returns(true);

        RedisApiKeyService service = new(_redis, _logger);

        ApiKeyCreateResult result = await service.CreateApiKeyAsync("Key", userId, tenantId);

        result.Success.Should().BeTrue();
        result.Prefix.Should().HaveLength(16);
        result.Prefix.Should().StartWith("sk_live_");
    }

    // CreateApiKeyAsync: stores metadata by both hash and keyId
    [Fact]
    public async Task CreateApiKeyAsync_Success_StoresByHashAndById()
    {
        Guid userId = Guid.NewGuid();
        Guid tenantId = Guid.NewGuid();

        _db.StringSetAsync(Arg.Any<RedisKey>(), Arg.Any<RedisValue>(), Arg.Any<TimeSpan?>(),
                Arg.Any<bool>(), Arg.Any<When>(), Arg.Any<CommandFlags>())
            .Returns(true);
        _db.SetAddAsync(Arg.Any<RedisKey>(), Arg.Any<RedisValue>(), Arg.Any<CommandFlags>())
            .Returns(true);

        RedisApiKeyService service = new(_redis, _logger);

        await service.CreateApiKeyAsync("Key", userId, tenantId);

        // Two StringSetAsync calls: one for hash lookup, one for id lookup
        await _db.Received(2).StringSetAsync(
            Arg.Is<RedisKey>(k => k.ToString().StartsWith("apikey:")),
            Arg.Any<RedisValue>(),
            Arg.Any<TimeSpan?>(),
            Arg.Any<bool>(),
            Arg.Any<When>(),
            Arg.Any<CommandFlags>());
    }

    // ListApiKeysAsync: maps all metadata fields correctly
    [Fact]
    public async Task ListApiKeysAsync_MapsAllFields_Correctly()
    {
        Guid userId = Guid.NewGuid();
        Guid tenantId = Guid.NewGuid();
        DateTimeOffset createdAt = DateTimeOffset.UtcNow.AddDays(-5);
        DateTimeOffset expiresAt = DateTimeOffset.UtcNow.AddDays(25);
        DateTimeOffset lastUsedAt = DateTimeOffset.UtcNow.AddHours(-1);

        string keyJson = JsonSerializer.Serialize(new
        {
            KeyId = "key-full",
            Name = "Full Key",
            Prefix = "sk_live_full123",
            KeyHash = "hash-full",
            UserId = userId,
            TenantId = tenantId,
            Scopes = new List<string> { "read", "write" },
            CreatedAt = createdAt,
            ExpiresAt = (DateTimeOffset?)expiresAt,
            LastUsedAt = (DateTimeOffset?)lastUsedAt
        });

        _db.SetMembersAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>())
            .Returns(new RedisValue[] { "key-full" });

        _db.StringGetAsync(Arg.Is<RedisKey>(k => k.ToString().Contains("id:key-full")), Arg.Any<CommandFlags>())
            .Returns((RedisValue)keyJson);

        RedisApiKeyService service = new(_redis, _logger);

        IReadOnlyList<ApiKeyMetadata> result = await service.ListApiKeysAsync(userId);

        result.Should().HaveCount(1);
        ApiKeyMetadata key = result[0];
        key.KeyId.Should().Be("key-full");
        key.Name.Should().Be("Full Key");
        key.Prefix.Should().Be("sk_live_full123");
        key.UserId.Should().Be(userId);
        key.TenantId.Should().Be(tenantId);
        key.Scopes.Should().BeEquivalentTo(_readWriteScopes);
        key.ExpiresAt.Should().NotBeNull();
        key.LastUsedAt.Should().NotBeNull();
    }
}
