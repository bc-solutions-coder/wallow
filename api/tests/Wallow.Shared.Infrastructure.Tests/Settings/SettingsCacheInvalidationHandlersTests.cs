using Microsoft.Extensions.Caching.Distributed;
using NSubstitute;
using Wallow.Shared.Infrastructure.Settings;
using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.Settings;

namespace Wallow.Shared.Infrastructure.Tests.Settings;

public class SettingsCacheInvalidationHandlersTests
{
    private readonly IDistributedCache _cache;

    public SettingsCacheInvalidationHandlersTests()
    {
        _cache = Substitute.For<IDistributedCache>();
    }

    [Fact]
    public async Task HandleAsync_TenantSettingChangedEvent_RemovesTenantCacheKey()
    {
        TenantId tenantId = TenantId.New();
        TenantSettingChangedEvent evt = new(tenantId, "some.key", "billing");

        await SettingsCacheInvalidationHandlers.HandleAsync(evt, _cache, CancellationToken.None);

        await _cache.Received(1).RemoveAsync($"settings:billing:{tenantId.Value}", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_TenantSettingChangedEvent_WithNullModule_RemovesCacheKeyWithNullModule()
    {
        TenantId tenantId = TenantId.New();
        TenantSettingChangedEvent evt = new(tenantId, "some.key", null);

        await SettingsCacheInvalidationHandlers.HandleAsync(evt, _cache, CancellationToken.None);

        await _cache.Received(1).RemoveAsync($"settings::{tenantId.Value}", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_UserSettingChangedEvent_RemovesUserCacheKey()
    {
        TenantId tenantId = TenantId.New();
        UserId userId = UserId.New();
        UserSettingChangedEvent evt = new(userId, tenantId, "some.key", "identity");

        await SettingsCacheInvalidationHandlers.HandleAsync(evt, _cache, CancellationToken.None);

        await _cache.Received(1).RemoveAsync($"settings:identity:{tenantId.Value}:{userId.Value}", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_UserSettingChangedEvent_WithNullModule_RemovesKeyWithNullModule()
    {
        TenantId tenantId = TenantId.New();
        UserId userId = UserId.New();
        UserSettingChangedEvent evt = new(userId, tenantId, "some.key", null);

        await SettingsCacheInvalidationHandlers.HandleAsync(evt, _cache, CancellationToken.None);

        await _cache.Received(1).RemoveAsync($"settings::{tenantId.Value}:{userId.Value}", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_TenantSettingChangedEvent_PassesCancellationToken()
    {
        TenantId tenantId = TenantId.New();
        TenantSettingChangedEvent evt = new(tenantId, "key", "mod");
        using CancellationTokenSource cts = new();
        CancellationToken ct = cts.Token;

        await SettingsCacheInvalidationHandlers.HandleAsync(evt, _cache, ct);

        await _cache.Received(1).RemoveAsync(Arg.Any<string>(), ct);
    }
}
