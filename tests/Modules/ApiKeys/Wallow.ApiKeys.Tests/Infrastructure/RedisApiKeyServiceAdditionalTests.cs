using System.Text.Json;
using Microsoft.Extensions.Logging;
using NSubstitute.ExceptionExtensions;
using StackExchange.Redis;
using Wallow.ApiKeys.Application.Interfaces;
using Wallow.ApiKeys.Infrastructure.Services;
using Wallow.Shared.Contracts.ApiKeys;

namespace Wallow.ApiKeys.Tests.Infrastructure;

public class RedisApiKeyServiceAdditionalTests
{
    private static readonly string[] _invoicesReadScope = ["invoices.read"];
    private readonly IRedisDatabase _db = Substitute.For<IRedisDatabase>();
    private readonly IApiKeyRepository _apiKeyRepository = Substitute.For<IApiKeyRepository>();
    private readonly ILogger<RedisApiKeyService> _logger = Substitute.For<ILogger<RedisApiKeyService>>();

    [Fact]
    public async Task ValidateApiKeyAsync_ValidKey_ReturnsValid()
    {
        Guid userId = Guid.NewGuid();
        Guid tenantId = Guid.NewGuid();

        string keyJson = JsonSerializer.Serialize(new
        {
            KeyId = "key-1",
            Name = "Test Key",
            Prefix = "sk_live_abc",
            KeyHash = "somehash",
            UserId = userId,
            TenantId = tenantId,
            Scopes = _invoicesReadScope,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-1),
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(30),
            LastUsedAt = (DateTimeOffset?)null
        });

        _db.StringGetAsync(Arg.Any<RedisKey>())
            .Returns((RedisValue)keyJson);

        _db.StringSetAsync(Arg.Any<RedisKey>(), Arg.Any<RedisValue>(), Arg.Any<TimeSpan?>(),
                Arg.Any<bool>(), Arg.Any<When>(), Arg.Any<CommandFlags>())
            .Returns(true);

        RedisApiKeyService service = new(_db, _apiKeyRepository, TimeProvider.System, _logger);

        ApiKeyValidationResult result = await service.ValidateApiKeyAsync("sk_live_somekeydata123456");

        result.IsValid.Should().BeTrue();
        result.KeyId.Should().Be("key-1");
        result.UserId.Should().Be(userId);
        result.TenantId.Should().Be(tenantId);
        result.Scopes.Should().Contain("invoices.read");
    }

    [Fact]
    public async Task ValidateApiKeyAsync_ExpiredKey_ReturnsInvalid()
    {
        Guid userId = Guid.NewGuid();
        Guid tenantId = Guid.NewGuid();

        string keyJson = JsonSerializer.Serialize(new
        {
            KeyId = "key-expired",
            Name = "Expired Key",
            Prefix = "sk_live_abc",
            KeyHash = "somehash",
            UserId = userId,
            TenantId = tenantId,
            Scopes = _invoicesReadScope,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-60),
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(-1),
            LastUsedAt = (DateTimeOffset?)null
        });

        _db.StringGetAsync(Arg.Any<RedisKey>())
            .Returns((RedisValue)keyJson);

        RedisApiKeyService service = new(_db, _apiKeyRepository, TimeProvider.System, _logger);

        ApiKeyValidationResult result = await service.ValidateApiKeyAsync("sk_live_somekeydata123456");

        result.IsValid.Should().BeFalse();
        result.KeyId.Should().Be("key-expired");
        result.Error.Should().Contain("expired");
    }

    [Fact]
    public async Task ValidateApiKeyAsync_InvalidData_ReturnsInvalid()
    {
        _db.StringGetAsync(Arg.Any<RedisKey>())
            .Returns((RedisValue)"not-valid-json{{{");

        RedisApiKeyService service = new(_db, _apiKeyRepository, TimeProvider.System, _logger);

        ApiKeyValidationResult result = await service.ValidateApiKeyAsync("sk_live_somekeydata123456");

        // Depending on deserialization error handling
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateApiKeyAsync_NullDeserializedData_ReturnsInvalid()
    {
        _db.StringGetAsync(Arg.Any<RedisKey>())
            .Returns((RedisValue)"null");

        RedisApiKeyService service = new(_db, _apiKeyRepository, TimeProvider.System, _logger);

        ApiKeyValidationResult result = await service.ValidateApiKeyAsync("sk_live_somekeydata123456");

        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("Invalid API key data");
    }

    [Fact]
    public async Task GetApiKeyCountAsync_WhenRedisReturnsCount_ReturnsCastAsInt()
    {
        Guid userId = Guid.NewGuid();

        _db.SetLengthAsync(Arg.Any<RedisKey>())
            .Returns(5L);

        RedisApiKeyService service = new(_db, _apiKeyRepository, TimeProvider.System, _logger);

        int result = await service.GetApiKeyCountAsync(userId);

        result.Should().Be(5);
    }

    [Fact]
    public async Task GetApiKeyCountAsync_WhenRedisThrows_ReturnsZero()
    {
        Guid userId = Guid.NewGuid();

        _db.SetLengthAsync(Arg.Any<RedisKey>())
            .Throws(new RedisException("Connection lost"));

        RedisApiKeyService service = new(_db, _apiKeyRepository, TimeProvider.System, _logger);

        int result = await service.GetApiKeyCountAsync(userId);

        result.Should().Be(0);
    }

    [Fact]
    public async Task ValidateApiKeyAsync_NoExpiration_ReturnsValid()
    {
        Guid userId = Guid.NewGuid();
        Guid tenantId = Guid.NewGuid();

        string keyJson = JsonSerializer.Serialize(new
        {
            KeyId = "key-noexpiry",
            Name = "No Expiry Key",
            Prefix = "sk_live_abc",
            KeyHash = "somehash",
            UserId = userId,
            TenantId = tenantId,
            Scopes = new List<string>(),
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-1),
            ExpiresAt = (DateTimeOffset?)null,
            LastUsedAt = (DateTimeOffset?)null
        });

        _db.StringGetAsync(Arg.Any<RedisKey>())
            .Returns((RedisValue)keyJson);

        _db.StringSetAsync(Arg.Any<RedisKey>(), Arg.Any<RedisValue>(), Arg.Any<TimeSpan?>(),
                Arg.Any<bool>(), Arg.Any<When>(), Arg.Any<CommandFlags>())
            .Returns(true);

        RedisApiKeyService service = new(_db, _apiKeyRepository, TimeProvider.System, _logger);

        ApiKeyValidationResult result = await service.ValidateApiKeyAsync("sk_live_somekeydata123456");

        result.IsValid.Should().BeTrue();
        result.KeyId.Should().Be("key-noexpiry");
    }
}
