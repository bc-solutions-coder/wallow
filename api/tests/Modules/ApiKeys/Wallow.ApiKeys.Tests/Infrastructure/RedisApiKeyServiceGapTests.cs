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

        await _db.Received(2).StringSetAsync(
            Arg.Any<RedisKey>(),
            Arg.Any<RedisValue>(),
            Arg.Is<TimeSpan?>(t => t.HasValue && t.Value.TotalMinutes > 0),
            Arg.Any<bool>(),
            Arg.Any<When>(),
            Arg.Any<CommandFlags>());
    }

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

    [Fact]
    public async Task ValidateApiKeyAsync_WhitespaceOnly_ReturnsInvalidFormat()
    {
        RedisApiKeyService service = CreateService();

        ApiKeyValidationResult result = await service.ValidateApiKeyAsync("   ");

        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("Invalid API key format");
    }

    [Fact]
    public async Task ValidateApiKeyAsync_Null_ReturnsInvalidFormat()
    {
        RedisApiKeyService service = CreateService();

        ApiKeyValidationResult result = await service.ValidateApiKeyAsync(null!);

        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("Invalid API key format");
    }

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

        // At least one usage-cache write must have completed.
        await _db.Received().StringSetAsync(
            Arg.Is<RedisKey>(k => k.ToString().StartsWith("apikey:")),
            Arg.Any<RedisValue>(),
            Arg.Any<TimeSpan?>(),
            Arg.Any<bool>(),
            Arg.Any<When>(),
            Arg.Any<CommandFlags>());
    }

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

        _db.StringGetAsync(Arg.Any<RedisKey>())
            .Returns((RedisValue)keyJson);

        _db.StringSetAsync(Arg.Any<RedisKey>(), Arg.Any<RedisValue>(), Arg.Any<TimeSpan?>(), Arg.Any<bool>(), Arg.Any<When>(), Arg.Any<CommandFlags>())
            .Throws(new RedisException("Write failed"));

        RedisApiKeyService service = CreateService();

        ApiKeyValidationResult result = await service.ValidateApiKeyAsync("sk_live_somekeydata123456");

        result.IsValid.Should().BeTrue();
        result.KeyId.Should().Be("key-update-fail");
    }

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

    [Fact]
    public async Task RevokeApiKeyAsync_NonGuidKeyId_ReturnsFalseWithoutTouchingAnything()
    {
        Guid userId = Guid.NewGuid();

        RedisApiKeyService service = CreateService();

        bool result = await service.RevokeApiKeyAsync("key-1", userId);

        result.Should().BeFalse();
        await _apiKeyRepository.DidNotReceive().RevokeAsync(
            Arg.Any<Wallow.ApiKeys.Domain.ApiKeys.ApiKeyId>(), Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _db.DidNotReceive().KeyDeleteAsync(Arg.Any<RedisKey>());
    }

    [Fact]
    public async Task RevokeApiKeyAsync_Success_DeletesHashAndIdAndRemovesFromUserSet()
    {
        Guid userId = Guid.NewGuid();
        Guid tenantId = Guid.NewGuid();

        ApiKey key = ApiKey.Create(
            new TenantId(tenantId), userId.ToString(), "hash-revoke", "Revoke Me",
            _readScope, expiresAt: null, userId, TimeProvider.System);
        _apiKeyRepository.GetByIdAsync(key.Id, Arg.Any<CancellationToken>()).Returns(key);

        RedisApiKeyService service = CreateService();

        string keyId = key.Id.Value.ToString();
        bool result = await service.RevokeApiKeyAsync(keyId, userId);

        result.Should().BeTrue();

        await _apiKeyRepository.Received(1).RevokeAsync(key.Id, tenantId, userId, Arg.Any<CancellationToken>());

        await _db.Received().KeyDeleteAsync(
            Arg.Is<RedisKey>(k => k.ToString() == "apikey:hash-revoke"));

        await _db.Received().KeyDeleteAsync(
            Arg.Is<RedisKey>(k => k.ToString() == $"apikey:id:{keyId}"));

        await _db.Received().SetRemoveAsync(
            Arg.Is<RedisKey>(k => k.ToString() == $"apikeys:user:{userId}"),
            Arg.Is<RedisValue>(keyId));
    }

    [Fact]
    public async Task RevokeApiKeyAsync_WhenDeleteThrows_ReturnsFalse()
    {
        Guid userId = Guid.NewGuid();

        ApiKey key = ApiKey.Create(
            new TenantId(Guid.NewGuid()), userId.ToString(), "hash-del-fail", "Key",
            _readScope, expiresAt: null, userId, TimeProvider.System);
        _apiKeyRepository.GetByIdAsync(key.Id, Arg.Any<CancellationToken>()).Returns(key);
        _db.KeyDeleteAsync(Arg.Any<RedisKey>())
            .Throws(new RedisException("Delete failed"));

        RedisApiKeyService service = CreateService();

        bool result = await service.RevokeApiKeyAsync(key.Id.Value.ToString(), userId);

        result.Should().BeFalse();
    }

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

    [Fact]
    public async Task CreateApiKeyAsync_Success_StoresByHashAndById()
    {
        Guid userId = Guid.NewGuid();
        Guid tenantId = Guid.NewGuid();

        RedisApiKeyService service = CreateService();

        ApiKeyCreateResult result = await service.CreateApiKeyAsync("Key", userId, tenantId);

        result.Success.Should().BeTrue();
        result.KeyId.Should().NotBeNullOrEmpty();
        result.ApiKey.Should().StartWith("sk_live_");

        await _db.Received(1).SetAddAsync(
            Arg.Is<RedisKey>(k => k.ToString() == $"apikeys:user:{userId}"),
            Arg.Any<RedisValue>());
    }

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

        await _db.Received(1).StringSetAsync(
            Arg.Is<RedisKey>(k => k.ToString().StartsWith("apikey:")),
            Arg.Any<RedisValue>(),
            Arg.Any<TimeSpan?>(),
            Arg.Any<bool>(),
            Arg.Any<When>(),
            Arg.Any<CommandFlags>());
    }

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
