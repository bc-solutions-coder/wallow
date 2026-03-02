using System.Text;
using System.Text.Json;
using Foundry.Configuration.Application.FeatureFlags.Contracts;
using Foundry.Configuration.Infrastructure.Services;
using Microsoft.Extensions.Caching.Distributed;

namespace Foundry.Configuration.Tests.Infrastructure;

public class CachedFeatureFlagServiceIsEnabledTests
{
    private readonly IFeatureFlagService _inner;
    private readonly IDistributedCache _cache;
    private readonly CachedFeatureFlagService _service;

    public CachedFeatureFlagServiceIsEnabledTests()
    {
        _inner = Substitute.For<IFeatureFlagService>();
        _cache = Substitute.For<IDistributedCache>();
        _service = new CachedFeatureFlagService(_inner, _cache);
    }

    [Fact]
    public async Task IsEnabledAsync_WhenCacheMiss_CallsInnerService()
    {
        Guid tenantId = Guid.NewGuid();
        _cache.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((byte[]?)null);
        _inner.IsEnabledAsync("feature", tenantId, null, Arg.Any<CancellationToken>()).Returns(true);

        bool result = await _service.IsEnabledAsync("feature", tenantId);

        result.Should().BeTrue();
        await _inner.Received(1).IsEnabledAsync("feature", tenantId, null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task IsEnabledAsync_WhenCacheMiss_StoresResultInCache()
    {
        Guid tenantId = Guid.NewGuid();
        _cache.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((byte[]?)null);
        _inner.IsEnabledAsync("feature", tenantId, null, Arg.Any<CancellationToken>()).Returns(true);

        await _service.IsEnabledAsync("feature", tenantId);

        await _cache.Received(1).SetAsync(
            Arg.Any<string>(),
            Arg.Any<byte[]>(),
            Arg.Any<DistributedCacheEntryOptions>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task IsEnabledAsync_WhenCacheHit_DoesNotCallInnerService()
    {
        Guid tenantId = Guid.NewGuid();
        string cachedValue = JsonSerializer.Serialize(true);
        _cache.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Encoding.UTF8.GetBytes(cachedValue));

        bool result = await _service.IsEnabledAsync("feature", tenantId);

        result.Should().BeTrue();
        await _inner.DidNotReceive().IsEnabledAsync(
            Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task IsEnabledAsync_CacheKeyIncludesUserId()
    {
        Guid tenantId = Guid.NewGuid();
        Guid userId = Guid.NewGuid();
        _cache.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((byte[]?)null);
        _inner.IsEnabledAsync("feature", tenantId, userId, Arg.Any<CancellationToken>()).Returns(false);

        await _service.IsEnabledAsync("feature", tenantId, userId);

        string expectedKey = $"ff:feature:{tenantId}:{userId}";
        await _cache.Received().GetAsync(expectedKey, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task IsEnabledAsync_CacheKeyWithoutUserId()
    {
        Guid tenantId = Guid.NewGuid();
        _cache.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((byte[]?)null);
        _inner.IsEnabledAsync("feature", tenantId, null, Arg.Any<CancellationToken>()).Returns(true);

        await _service.IsEnabledAsync("feature", tenantId);

        string expectedKey = $"ff:feature:{tenantId}:";
        await _cache.Received().GetAsync(expectedKey, Arg.Any<CancellationToken>());
    }
}

public class CachedFeatureFlagServiceGetVariantTests
{
    private readonly IFeatureFlagService _inner;
    private readonly IDistributedCache _cache;
    private readonly CachedFeatureFlagService _service;

    public CachedFeatureFlagServiceGetVariantTests()
    {
        _inner = Substitute.For<IFeatureFlagService>();
        _cache = Substitute.For<IDistributedCache>();
        _service = new CachedFeatureFlagService(_inner, _cache);
    }

    [Fact]
    public async Task GetVariantAsync_WhenCacheMiss_CallsInnerService()
    {
        Guid tenantId = Guid.NewGuid();
        _cache.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((byte[]?)null);
        _inner.GetVariantAsync("ab_test", tenantId, null, Arg.Any<CancellationToken>()).Returns("control");

        string? result = await _service.GetVariantAsync("ab_test", tenantId);

        result.Should().Be("control");
        await _inner.Received(1).GetVariantAsync("ab_test", tenantId, null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetVariantAsync_WhenCacheHit_ReturnsCachedVariant()
    {
        Guid tenantId = Guid.NewGuid();
        string cachedValue = JsonSerializer.Serialize("treatment");
        _cache.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Encoding.UTF8.GetBytes(cachedValue));

        string? result = await _service.GetVariantAsync("ab_test", tenantId);

        result.Should().Be("treatment");
        await _inner.DidNotReceive().GetVariantAsync(
            Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetVariantAsync_WhenCacheMiss_StoresResultInCache()
    {
        Guid tenantId = Guid.NewGuid();
        _cache.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((byte[]?)null);
        _inner.GetVariantAsync("ab_test", tenantId, null, Arg.Any<CancellationToken>()).Returns("control");

        await _service.GetVariantAsync("ab_test", tenantId);

        await _cache.Received(1).SetAsync(
            Arg.Any<string>(),
            Arg.Any<byte[]>(),
            Arg.Any<DistributedCacheEntryOptions>(),
            Arg.Any<CancellationToken>());
    }
}

public class CachedFeatureFlagServiceGetAllFlagsTests
{
    private readonly IFeatureFlagService _inner;
    private readonly IDistributedCache _cache;
    private readonly CachedFeatureFlagService _service;

    public CachedFeatureFlagServiceGetAllFlagsTests()
    {
        _inner = Substitute.For<IFeatureFlagService>();
        _cache = Substitute.For<IDistributedCache>();
        _service = new CachedFeatureFlagService(_inner, _cache);
    }

    [Fact]
    public async Task GetAllFlagsAsync_DelegatesToInnerDirectly()
    {
        Guid tenantId = Guid.NewGuid();
        Guid userId = Guid.NewGuid();
        Dictionary<string, object> expected = new() { ["flag1"] = true };
        _inner.GetAllFlagsAsync(tenantId, userId, Arg.Any<CancellationToken>()).Returns(expected);

        Dictionary<string, object> result = await _service.GetAllFlagsAsync(tenantId, userId);

        result.Should().BeSameAs(expected);
    }
}

public class CachedFeatureFlagServiceInvalidateTests
{
    [Fact]
    public async Task InvalidateAsync_RemovesTrackedCacheKeys()
    {
        IFeatureFlagService inner = Substitute.For<IFeatureFlagService>();
        IDistributedCache cache = Substitute.For<IDistributedCache>();
        CachedFeatureFlagService service = new(inner, cache);

        Guid tenantId = Guid.NewGuid();
        string flagKey = $"invalidate_test_{tenantId}";

        // Trigger a cache lookup to track the key
        cache.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((byte[]?)null);
        inner.IsEnabledAsync(flagKey, tenantId, null, Arg.Any<CancellationToken>()).Returns(true);
        await service.IsEnabledAsync(flagKey, tenantId);

        // Now invalidate should remove the tracked cache key
        await CachedFeatureFlagService.InvalidateAsync(cache, flagKey);

        string expectedCacheKey = $"ff:{flagKey}:{tenantId}:";
        await cache.Received().RemoveAsync(expectedCacheKey, Arg.Any<CancellationToken>());
    }
}
