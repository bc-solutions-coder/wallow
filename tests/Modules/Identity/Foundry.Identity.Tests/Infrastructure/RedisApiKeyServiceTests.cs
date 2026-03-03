using Foundry.Identity.Application.Interfaces;
using Foundry.Identity.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using NSubstitute.ExceptionExtensions;
using StackExchange.Redis;

namespace Foundry.Identity.Tests.Infrastructure;

public class RedisApiKeyServiceTests
{
    private static readonly string[] _invoicesReadScope = ["invoices.read"];
    private readonly IConnectionMultiplexer _redis = Substitute.For<IConnectionMultiplexer>();
    private readonly IDatabase _db = Substitute.For<IDatabase>();
    private readonly ILogger<RedisApiKeyService> _logger = Substitute.For<ILogger<RedisApiKeyService>>();

    public RedisApiKeyServiceTests()
    {
        _redis.GetDatabase(Arg.Any<int>(), Arg.Any<object?>()).Returns(_db);
        _redis.GetDatabase().Returns(_db);
        _db.StringSetAsync(Arg.Any<RedisKey>(), Arg.Any<RedisValue>(), Arg.Any<TimeSpan?>(), Arg.Any<bool>(), Arg.Any<When>(), Arg.Any<CommandFlags>()).Returns(true);
        _db.SetAddAsync(Arg.Any<RedisKey>(), Arg.Any<RedisValue>(), Arg.Any<CommandFlags>()).Returns(true);
    }

    [Fact(Skip = "NSubstitute cannot match StackExchange.Redis 2.8 StringSetAsync overload")]
    public async Task CreateApiKeyAsync_Success_ReturnsKeyWithSkPrefix()
    {
        Guid userId = Guid.NewGuid();
        Guid tenantId = Guid.NewGuid();

        _db.StringSetAsync(Arg.Any<RedisKey>(), Arg.Any<RedisValue>(), Arg.Any<TimeSpan?>(),
                Arg.Any<bool>(), Arg.Any<When>(), Arg.Any<CommandFlags>())
            .Returns(true);
        _db.SetAddAsync(Arg.Any<RedisKey>(), Arg.Any<RedisValue>(), Arg.Any<CommandFlags>())
            .Returns(true);

        RedisApiKeyService service = new(_redis, _logger);

        ApiKeyCreateResult result = await service.CreateApiKeyAsync(
            "Test Key", userId, tenantId, _invoicesReadScope);

        result.Success.Should().BeTrue();
        result.ApiKey.Should().StartWith("sk_live_");
        result.KeyId.Should().NotBeNullOrEmpty();
        result.Prefix.Should().StartWith("sk_live_");
        result.Error.Should().BeNull();

        // Verify Redis writes
        await _db.Received().StringSetAsync(
            Arg.Is<RedisKey>(k => k.ToString().StartsWith("apikey:")),
            Arg.Any<RedisValue>(),
            Arg.Any<TimeSpan?>(),
            Arg.Any<bool>(),
            Arg.Any<When>(),
            Arg.Any<CommandFlags>());
    }

    [Fact(Skip = "StackExchange.Redis StringSetAsync overload mismatch with NSubstitute mock")]
    public async Task CreateApiKeyAsync_WithExpiration_SetsExpiry()
    {
        Guid userId = Guid.NewGuid();
        Guid tenantId = Guid.NewGuid();
        DateTimeOffset expiresAt = DateTimeOffset.UtcNow.AddDays(30);

        _db.StringSetAsync(Arg.Any<RedisKey>(), Arg.Any<RedisValue>(), Arg.Any<TimeSpan?>(),
                Arg.Any<bool>(), Arg.Any<When>(), Arg.Any<CommandFlags>())
            .Returns(true);
        _db.SetAddAsync(Arg.Any<RedisKey>(), Arg.Any<RedisValue>(), Arg.Any<CommandFlags>())
            .Returns(true);

        RedisApiKeyService service = new(_redis, _logger);

        ApiKeyCreateResult result = await service.CreateApiKeyAsync(
            "Expiring Key", userId, tenantId, null, expiresAt);

        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task CreateApiKeyAsync_WhenRedisThrows_ReturnsFailure()
    {
        Guid userId = Guid.NewGuid();
        Guid tenantId = Guid.NewGuid();

        _db.StringSetAsync(
                Arg.Any<RedisKey>(), Arg.Any<RedisValue>(), Arg.Any<TimeSpan?>(),
                Arg.Any<bool>(), Arg.Any<When>(), Arg.Any<CommandFlags>())
            .Throws(new RedisException("Connection lost"));

        RedisApiKeyService service = new(_redis, _logger);

        ApiKeyCreateResult result = await service.CreateApiKeyAsync(
            "Test Key", userId, tenantId);

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("Failed");
    }

    [Fact]
    public async Task ValidateApiKeyAsync_NullOrEmpty_ReturnsInvalid()
    {
        RedisApiKeyService service = new(_redis, _logger);

        ApiKeyValidationResult result = await service.ValidateApiKeyAsync("");

        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("Invalid API key format");
    }

    [Fact]
    public async Task ValidateApiKeyAsync_WrongPrefix_ReturnsInvalid()
    {
        RedisApiKeyService service = new(_redis, _logger);

        ApiKeyValidationResult result = await service.ValidateApiKeyAsync("pk_test_something");

        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("Invalid API key format");
    }

    [Fact]
    public async Task ValidateApiKeyAsync_KeyNotInRedis_ReturnsNotFound()
    {
        _db.StringGetAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>())
            .Returns(RedisValue.Null);

        RedisApiKeyService service = new(_redis, _logger);

        ApiKeyValidationResult result = await service.ValidateApiKeyAsync("sk_live_somekeydata123456");

        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public async Task ValidateApiKeyAsync_WhenRedisThrows_ReturnsError()
    {
        _db.StringGetAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>())
            .Throws(new RedisException("Connection failed"));

        RedisApiKeyService service = new(_redis, _logger);

        ApiKeyValidationResult result = await service.ValidateApiKeyAsync("sk_live_somekeydata123456");

        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("Validation error");
    }

    [Fact]
    public async Task ListApiKeysAsync_ReturnsKeys()
    {
        Guid userId = Guid.NewGuid();
        Guid tenantId = Guid.NewGuid();

        string keyJson = System.Text.Json.JsonSerializer.Serialize(new
        {
            KeyId = "key-1",
            Name = "Test Key",
            Prefix = "sk_live_abc",
            KeyHash = "hash-1",
            UserId = userId,
            TenantId = tenantId,
            Scopes = _invoicesReadScope,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-1),
            ExpiresAt = (DateTimeOffset?)null,
            LastUsedAt = (DateTimeOffset?)null
        });

        _db.SetMembersAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>())
            .Returns(new RedisValue[] { "key-1" });

        _db.StringGetAsync(Arg.Is<RedisKey>(k => k.ToString().Contains("id:key-1")), Arg.Any<CommandFlags>())
            .Returns((RedisValue)keyJson);

        RedisApiKeyService service = new(_redis, _logger);

        IReadOnlyList<ApiKeyMetadata> result = await service.ListApiKeysAsync(userId);

        result.Should().HaveCount(1);
        result[0].KeyId.Should().Be("key-1");
        result[0].Name.Should().Be("Test Key");
    }

    [Fact]
    public async Task ListApiKeysAsync_WhenRedisThrows_ReturnsEmpty()
    {
        Guid userId = Guid.NewGuid();

        _db.SetMembersAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>())
            .Throws(new RedisException("Connection lost"));

        RedisApiKeyService service = new(_redis, _logger);

        IReadOnlyList<ApiKeyMetadata> result = await service.ListApiKeysAsync(userId);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ListApiKeysAsync_SkipsKeysWithNullData()
    {
        Guid userId = Guid.NewGuid();

        _db.SetMembersAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>())
            .Returns(new RedisValue[] { "key-1", "key-2" });

        _db.StringGetAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>())
            .Returns(RedisValue.Null);

        RedisApiKeyService service = new(_redis, _logger);

        IReadOnlyList<ApiKeyMetadata> result = await service.ListApiKeysAsync(userId);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task RevokeApiKeyAsync_WhenKeyNotFound_ReturnsFalse()
    {
        Guid userId = Guid.NewGuid();

        _db.StringGetAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>())
            .Returns(RedisValue.Null);

        RedisApiKeyService service = new(_redis, _logger);

        bool result = await service.RevokeApiKeyAsync("nonexistent", userId);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task RevokeApiKeyAsync_WhenUserMismatch_ReturnsFalse()
    {
        Guid userId = Guid.NewGuid();
        Guid otherUserId = Guid.NewGuid();

        string keyJson = System.Text.Json.JsonSerializer.Serialize(new
        {
            KeyId = "key-1",
            Name = "Test Key",
            Prefix = "sk_live_abc",
            KeyHash = "hash-1",
            UserId = otherUserId,
            TenantId = Guid.NewGuid(),
            Scopes = Array.Empty<string>(),
            CreatedAt = DateTimeOffset.UtcNow,
        });

        _db.StringGetAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>())
            .Returns((RedisValue)keyJson);

        RedisApiKeyService service = new(_redis, _logger);

        bool result = await service.RevokeApiKeyAsync("key-1", userId);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task RevokeApiKeyAsync_Success_ReturnsTrue()
    {
        Guid userId = Guid.NewGuid();

        string keyJson = System.Text.Json.JsonSerializer.Serialize(new
        {
            KeyId = "key-1",
            Name = "Test Key",
            Prefix = "sk_live_abc",
            KeyHash = "hash-1",
            UserId = userId,
            TenantId = Guid.NewGuid(),
            Scopes = Array.Empty<string>(),
            CreatedAt = DateTimeOffset.UtcNow,
        });

        _db.StringGetAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>())
            .Returns((RedisValue)keyJson);

        RedisApiKeyService service = new(_redis, _logger);

        bool result = await service.RevokeApiKeyAsync("key-1", userId);

        result.Should().BeTrue();
        await _db.Received().KeyDeleteAsync(Arg.Is<RedisKey>(k => k.ToString().Contains("hash-1")), Arg.Any<CommandFlags>());
        await _db.Received().KeyDeleteAsync(Arg.Is<RedisKey>(k => k.ToString().Contains("id:key-1")), Arg.Any<CommandFlags>());
        await _db.Received().SetRemoveAsync(Arg.Any<RedisKey>(), Arg.Is<RedisValue>("key-1"), Arg.Any<CommandFlags>());
    }

    [Fact]
    public async Task RevokeApiKeyAsync_WhenRedisThrows_ReturnsFalse()
    {
        Guid userId = Guid.NewGuid();

        _db.StringGetAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>())
            .Throws(new RedisException("Connection lost"));

        RedisApiKeyService service = new(_redis, _logger);

        bool result = await service.RevokeApiKeyAsync("key-1", userId);

        result.Should().BeFalse();
    }
}
