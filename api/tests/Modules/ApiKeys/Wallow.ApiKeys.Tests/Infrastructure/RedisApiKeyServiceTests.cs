using Microsoft.Extensions.Logging;
using NSubstitute.ExceptionExtensions;
using StackExchange.Redis;
using Wallow.ApiKeys.Application.Interfaces;
using Wallow.ApiKeys.Domain.Entities;
using Wallow.ApiKeys.Infrastructure.Services;
using Wallow.Shared.Contracts.ApiKeys;
using Wallow.Shared.Kernel.Identity;

namespace Wallow.ApiKeys.Tests.Infrastructure;

public class RedisApiKeyServiceTests
{
    private static readonly string[] _invoicesReadScope = ["invoices.read"];
    private readonly IRedisDatabase _db = Substitute.For<IRedisDatabase>();
    private readonly IApiKeyRepository _apiKeyRepository = Substitute.For<IApiKeyRepository>();
    private readonly ILogger<RedisApiKeyService> _logger = Substitute.For<ILogger<RedisApiKeyService>>();

    public RedisApiKeyServiceTests()
    {
        _db.StringSetAsync(Arg.Any<RedisKey>(), Arg.Any<RedisValue>(), Arg.Any<TimeSpan?>(), Arg.Any<bool>(), Arg.Any<When>(), Arg.Any<CommandFlags>()).Returns(true);
        _db.SetAddAsync(Arg.Any<RedisKey>(), Arg.Any<RedisValue>()).Returns(true);
    }

    [Fact]
    public async Task CreateApiKeyAsync_Success_ReturnsKeyWithSkPrefix()
    {
        Guid userId = Guid.NewGuid();
        Guid tenantId = Guid.NewGuid();

        _db.StringSetAsync(Arg.Any<RedisKey>(), Arg.Any<RedisValue>(), Arg.Any<TimeSpan?>(),
                Arg.Any<bool>(), Arg.Any<When>(), Arg.Any<CommandFlags>())
            .Returns(true);
        _db.SetAddAsync(Arg.Any<RedisKey>(), Arg.Any<RedisValue>())
            .Returns(true);

        RedisApiKeyService service = new(_db, _apiKeyRepository, TimeProvider.System, _logger);

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

    [Fact]
    public async Task CreateApiKeyAsync_WithExpiration_SetsExpiry()
    {
        Guid userId = Guid.NewGuid();
        Guid tenantId = Guid.NewGuid();
        DateTimeOffset expiresAt = DateTimeOffset.UtcNow.AddDays(30);

        _db.StringSetAsync(Arg.Any<RedisKey>(), Arg.Any<RedisValue>(), Arg.Any<TimeSpan?>(),
                Arg.Any<bool>(), Arg.Any<When>(), Arg.Any<CommandFlags>())
            .Returns(true);
        _db.SetAddAsync(Arg.Any<RedisKey>(), Arg.Any<RedisValue>())
            .Returns(true);

        RedisApiKeyService service = new(_db, _apiKeyRepository, TimeProvider.System, _logger);

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

        RedisApiKeyService service = new(_db, _apiKeyRepository, TimeProvider.System, _logger);

        ApiKeyCreateResult result = await service.CreateApiKeyAsync(
            "Test Key", userId, tenantId);

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("Failed");
    }

    [Fact]
    public async Task ValidateApiKeyAsync_NullOrEmpty_ReturnsInvalid()
    {
        RedisApiKeyService service = new(_db, _apiKeyRepository, TimeProvider.System, _logger);

        ApiKeyValidationResult result = await service.ValidateApiKeyAsync("");

        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("Invalid API key format");
    }

    [Fact]
    public async Task ValidateApiKeyAsync_WrongPrefix_ReturnsInvalid()
    {
        RedisApiKeyService service = new(_db, _apiKeyRepository, TimeProvider.System, _logger);

        ApiKeyValidationResult result = await service.ValidateApiKeyAsync("pk_test_something");

        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("Invalid API key format");
    }

    [Fact]
    public async Task ValidateApiKeyAsync_KeyNotInRedis_ReturnsNotFound()
    {
        _db.StringGetAsync(Arg.Any<RedisKey>())
            .Returns(RedisValue.Null);

        RedisApiKeyService service = new(_db, _apiKeyRepository, TimeProvider.System, _logger);

        ApiKeyValidationResult result = await service.ValidateApiKeyAsync("sk_live_somekeydata123456");

        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public async Task ValidateApiKeyAsync_WhenRedisThrows_ReturnsError()
    {
        _db.StringGetAsync(Arg.Any<RedisKey>())
            .Throws(new RedisException("Connection failed"));

        RedisApiKeyService service = new(_db, _apiKeyRepository, TimeProvider.System, _logger);

        ApiKeyValidationResult result = await service.ValidateApiKeyAsync("sk_live_somekeydata123456");

        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("Validation error");
    }

    [Fact]
    public async Task ListApiKeysAsync_ReturnsKeys()
    {
        Guid userId = Guid.NewGuid();
        Guid tenantId = Guid.NewGuid();

        ApiKey apiKey = ApiKey.Create(
            new TenantId(tenantId),
            userId.ToString(),
            "hash-1",
            "Test Key",
            _invoicesReadScope,
            null,
            userId,
            TimeProvider.System);

        _apiKeyRepository.ListByServiceAccountAsync(userId.ToString(), tenantId, Arg.Any<CancellationToken>())
            .Returns(new List<ApiKey> { apiKey });

        RedisApiKeyService service = new(_db, _apiKeyRepository, TimeProvider.System, _logger);

        IReadOnlyList<ApiKeyMetadata> result = await service.ListApiKeysAsync(userId, tenantId);

        result.Should().HaveCount(1);
        result[0].KeyId.Should().Be(apiKey.Id.Value.ToString());
        result[0].Name.Should().Be("Test Key");
    }

    [Fact]
    public async Task ListApiKeysAsync_WhenRepositoryThrows_ReturnsEmpty()
    {
        Guid userId = Guid.NewGuid();
        Guid tenantId = Guid.NewGuid();

        _apiKeyRepository.ListByServiceAccountAsync(userId.ToString(), tenantId, Arg.Any<CancellationToken>())
            .Throws(new RedisException("Connection lost"));

        RedisApiKeyService service = new(_db, _apiKeyRepository, TimeProvider.System, _logger);

        IReadOnlyList<ApiKeyMetadata> result = await service.ListApiKeysAsync(userId, tenantId);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ListApiKeysAsync_SkipsRevokedKeys()
    {
        Guid userId = Guid.NewGuid();
        Guid tenantId = Guid.NewGuid();

        _apiKeyRepository.ListByServiceAccountAsync(userId.ToString(), tenantId, Arg.Any<CancellationToken>())
            .Returns(new List<ApiKey>());

        RedisApiKeyService service = new(_db, _apiKeyRepository, TimeProvider.System, _logger);

        IReadOnlyList<ApiKeyMetadata> result = await service.ListApiKeysAsync(userId, tenantId);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task RevokeApiKeyAsync_WhenKeyNotFound_ReturnsFalse()
    {
        Guid userId = Guid.NewGuid();

        _apiKeyRepository.GetByIdAsync(Arg.Any<Wallow.ApiKeys.Domain.ApiKeys.ApiKeyId>(), Arg.Any<CancellationToken>())
            .Returns((ApiKey?)null);

        RedisApiKeyService service = new(_db, _apiKeyRepository, TimeProvider.System, _logger);

        bool result = await service.RevokeApiKeyAsync(Guid.NewGuid().ToString(), userId);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task RevokeApiKeyAsync_WhenUserMismatch_ReturnsFalse()
    {
        Guid userId = Guid.NewGuid();
        Guid otherUserId = Guid.NewGuid();

        ApiKey key = ApiKey.Create(
            new TenantId(Guid.NewGuid()), otherUserId.ToString(), "hash-1", "Test Key",
            _invoicesReadScope, expiresAt: null, otherUserId, TimeProvider.System);
        _apiKeyRepository.GetByIdAsync(key.Id, Arg.Any<CancellationToken>()).Returns(key);

        RedisApiKeyService service = new(_db, _apiKeyRepository, TimeProvider.System, _logger);

        bool result = await service.RevokeApiKeyAsync(key.Id.Value.ToString(), userId);

        result.Should().BeFalse();
        await _apiKeyRepository.DidNotReceive().RevokeAsync(
            Arg.Any<Wallow.ApiKeys.Domain.ApiKeys.ApiKeyId>(), Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _db.DidNotReceive().KeyDeleteAsync(Arg.Any<RedisKey>());
    }

    [Fact]
    public async Task RevokeApiKeyAsync_Success_ReturnsTrue()
    {
        Guid userId = Guid.NewGuid();
        Guid tenantId = Guid.NewGuid();

        ApiKey key = ApiKey.Create(
            new TenantId(tenantId), userId.ToString(), "hash-1", "Test Key",
            _invoicesReadScope, expiresAt: null, userId, TimeProvider.System);
        _apiKeyRepository.GetByIdAsync(key.Id, Arg.Any<CancellationToken>()).Returns(key);

        RedisApiKeyService service = new(_db, _apiKeyRepository, TimeProvider.System, _logger);

        string keyId = key.Id.Value.ToString();
        bool result = await service.RevokeApiKeyAsync(keyId, userId);

        result.Should().BeTrue();
        await _apiKeyRepository.Received(1).RevokeAsync(key.Id, tenantId, userId, Arg.Any<CancellationToken>());
        await _db.Received().KeyDeleteAsync(Arg.Is<RedisKey>(k => k.ToString().Contains("hash-1")));
        await _db.Received().KeyDeleteAsync(Arg.Is<RedisKey>(k => k.ToString().Contains($"id:{keyId}")));
        await _db.Received().SetRemoveAsync(Arg.Any<RedisKey>(), Arg.Is<RedisValue>(keyId));
    }

    [Fact]
    public async Task RevokeApiKeyAsync_WhenRedisThrows_ReturnsFalse()
    {
        Guid userId = Guid.NewGuid();

        ApiKey key = ApiKey.Create(
            new TenantId(Guid.NewGuid()), userId.ToString(), "hash-1", "Test Key",
            _invoicesReadScope, expiresAt: null, userId, TimeProvider.System);
        _apiKeyRepository.GetByIdAsync(key.Id, Arg.Any<CancellationToken>()).Returns(key);
        _db.KeyDeleteAsync(Arg.Any<RedisKey>())
            .Throws(new RedisException("Connection lost"));

        RedisApiKeyService service = new(_db, _apiKeyRepository, TimeProvider.System, _logger);

        bool result = await service.RevokeApiKeyAsync(key.Id.Value.ToString(), userId);

        result.Should().BeFalse();
    }
}
