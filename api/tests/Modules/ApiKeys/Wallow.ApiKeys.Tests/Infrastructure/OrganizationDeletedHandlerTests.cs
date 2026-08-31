using Microsoft.Extensions.Logging.Abstractions;
using StackExchange.Redis;
using Wallow.ApiKeys.Application.Interfaces;
using Wallow.ApiKeys.Domain.Entities;
using Wallow.ApiKeys.Infrastructure.Handlers;
using Wallow.ApiKeys.Infrastructure.Services;
using Wallow.Shared.Contracts.Identity.Events;
using Wallow.Shared.Kernel.Identity;

namespace Wallow.ApiKeys.Tests.Infrastructure;

/// <summary>
/// When Identity announces an organization's deletion, every API key in that tenant dies with
/// it: the PostgreSQL rows are revoked with the deleting actor's id, and the Valkey validation
/// entries are dropped so a key in flight stops validating immediately. Every cache name is
/// derived from the PostgreSQL row, so the drops never depend on what the cache still holds.
/// </summary>
public sealed class OrganizationDeletedHandlerTests
{
    private readonly IApiKeyRepository _repository = Substitute.For<IApiKeyRepository>();
    private readonly IRedisDatabase _redis = Substitute.For<IRedisDatabase>();
    private readonly OrganizationDeletedHandler _sut;
    private readonly Guid _orgId = Guid.NewGuid();
    private readonly Guid _actorId = Guid.NewGuid();
    private readonly Guid _keyOwnerId = Guid.NewGuid();

    public OrganizationDeletedHandlerTests()
    {
        _sut = new OrganizationDeletedHandler(
            _repository, _redis, NullLogger<OrganizationDeletedHandler>.Instance);
    }

    [Fact]
    public async Task HandleAsync_RevokesEveryLiveKeyAndDropsItsCacheEntries()
    {
        ApiKey key = CreateKey("hash-live");
        _repository.ListByTenantAsync(_orgId, Arg.Any<CancellationToken>()).Returns([key]);

        await _sut.HandleAsync(Deleted(), CancellationToken.None);

        _repository.Received(1).UseTenant(_orgId);
        await _repository.Received(1).RevokeAsync(key.Id, _orgId, _actorId, Arg.Any<CancellationToken>());
        await _redis.Received(1).KeyDeleteAsync("apikey:hash-live");
        await _redis.Received(1).KeyDeleteAsync($"apikey:id:{key.Id.Value}");
        await _redis.Received(1).SetRemoveAsync($"apikeys:user:{_keyOwnerId}", key.Id.Value.ToString());
    }

    [Fact]
    public async Task HandleAsync_SkipsRevokingAKeyAlreadyRevoked_ButStillDropsItsCache()
    {
        ApiKey key = CreateKey("hash-revoked");
        key.Revoke(_keyOwnerId, TimeProvider.System);
        _repository.ListByTenantAsync(_orgId, Arg.Any<CancellationToken>()).Returns([key]);

        await _sut.HandleAsync(Deleted(), CancellationToken.None);

        await _repository.DidNotReceive().RevokeAsync(
            Arg.Any<Wallow.ApiKeys.Domain.ApiKeys.ApiKeyId>(), Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _redis.Received(1).KeyDeleteAsync("apikey:hash-revoked");
    }

    [Fact]
    public async Task HandleAsync_WhenTheCacheEntryAlreadyExpired_StillRevokesTheRowAndDropsEveryName()
    {
        ApiKey key = CreateKey("hash-cold");
        _repository.ListByTenantAsync(_orgId, Arg.Any<CancellationToken>()).Returns([key]);

        await _sut.HandleAsync(Deleted(), CancellationToken.None);

        await _repository.Received(1).RevokeAsync(key.Id, _orgId, _actorId, Arg.Any<CancellationToken>());
        await _redis.Received(1).KeyDeleteAsync("apikey:hash-cold");
        await _redis.Received(1).KeyDeleteAsync($"apikey:id:{key.Id.Value}");
        await _redis.Received(1).SetRemoveAsync($"apikeys:user:{_keyOwnerId}", key.Id.Value.ToString());
        // The names come from the row, never from cached JSON — the cache is never even read.
        await _redis.DidNotReceive().StringGetAsync(Arg.Any<RedisKey>());
    }

    [Fact]
    public async Task HandleAsync_WithNoKeysInTheTenant_TouchesNothing()
    {
        _repository.ListByTenantAsync(_orgId, Arg.Any<CancellationToken>()).Returns([]);

        await _sut.HandleAsync(Deleted(), CancellationToken.None);

        await _repository.DidNotReceive().RevokeAsync(
            Arg.Any<Wallow.ApiKeys.Domain.ApiKeys.ApiKeyId>(), Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _redis.DidNotReceive().KeyDeleteAsync(Arg.Any<RedisKey>());
    }

    private ApiKey CreateKey(string hashedKey) => ApiKey.Create(
        new TenantId(_orgId),
        _keyOwnerId.ToString(),
        hashedKey,
        "Production Key",
        ["read"],
        expiresAt: null,
        _keyOwnerId,
        TimeProvider.System);

    private OrganizationDeletedEvent Deleted() => new()
    {
        OrganizationId = _orgId,
        TenantId = _orgId,
        OrganizationName = "Contoso",
        ActorId = _actorId,
        RecipientEmails = []
    };
}
