using System.Text.Json;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Wallow.Identity.Tests.Infrastructure;

public class RedisApiKeyServiceAdditionalTests
{
    private static readonly string[] _invoicesReadScope = ["invoices.read"];
    private readonly IConnectionMultiplexer _redis = Substitute.For<IConnectionMultiplexer>();
    private readonly IDatabase _db = Substitute.For<IDatabase>();
    private readonly IApiKeyRepository _apiKeyRepository = Substitute.For<IApiKeyRepository>();
    private readonly ILogger<RedisApiKeyService> _logger = Substitute.For<ILogger<RedisApiKeyService>>();

    public RedisApiKeyServiceAdditionalTests()
    {
        _redis.GetDatabase(Arg.Any<int>(), Arg.Any<object?>()).Returns(_db);
    }

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

        _db.StringGetAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>())
            .Returns((RedisValue)keyJson);

        _db.StringSetAsync(Arg.Any<RedisKey>(), Arg.Any<RedisValue>(), Arg.Any<TimeSpan?>(),
                Arg.Any<bool>(), Arg.Any<When>(), Arg.Any<CommandFlags>())
            .Returns(true);

        RedisApiKeyService service = new(_redis, _apiKeyRepository, TimeProvider.System, _logger);

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

        _db.StringGetAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>())
            .Returns((RedisValue)keyJson);

        RedisApiKeyService service = new(_redis, _apiKeyRepository, TimeProvider.System, _logger);

        ApiKeyValidationResult result = await service.ValidateApiKeyAsync("sk_live_somekeydata123456");

        result.IsValid.Should().BeFalse();
        result.KeyId.Should().Be("key-expired");
        result.Error.Should().Contain("expired");
    }

    [Fact]
    public async Task ValidateApiKeyAsync_InvalidData_ReturnsInvalid()
    {
        _db.StringGetAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>())
            .Returns((RedisValue)"not-valid-json{{{");

        RedisApiKeyService service = new(_redis, _apiKeyRepository, TimeProvider.System, _logger);

        ApiKeyValidationResult result = await service.ValidateApiKeyAsync("sk_live_somekeydata123456");

        // Depending on deserialization error handling
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateApiKeyAsync_NullDeserializedData_ReturnsInvalid()
    {
        _db.StringGetAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>())
            .Returns((RedisValue)"null");

        RedisApiKeyService service = new(_redis, _apiKeyRepository, TimeProvider.System, _logger);

        ApiKeyValidationResult result = await service.ValidateApiKeyAsync("sk_live_somekeydata123456");

        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("Invalid API key data");
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

        _db.StringGetAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>())
            .Returns((RedisValue)keyJson);

        _db.StringSetAsync(Arg.Any<RedisKey>(), Arg.Any<RedisValue>(), Arg.Any<TimeSpan?>(),
                Arg.Any<bool>(), Arg.Any<When>(), Arg.Any<CommandFlags>())
            .Returns(true);

        RedisApiKeyService service = new(_redis, _apiKeyRepository, TimeProvider.System, _logger);

        ApiKeyValidationResult result = await service.ValidateApiKeyAsync("sk_live_somekeydata123456");

        result.IsValid.Should().BeTrue();
        result.KeyId.Should().Be("key-noexpiry");
    }
}
