using Wallow.Shared.Infrastructure.Core.Cache;
using Microsoft.Extensions.Caching.Distributed;
using NSubstitute;

namespace Wallow.Shared.Infrastructure.Tests.Cache;

public class InstrumentedDistributedCacheTests
{
    private readonly IDistributedCache _inner;
    private readonly InstrumentedDistributedCache _sut;

    public InstrumentedDistributedCacheTests()
    {
        _inner = Substitute.For<IDistributedCache>();
        _sut = new InstrumentedDistributedCache(_inner);
    }

    [Fact]
    public void Get_WhenKeyExists_ReturnsValueAndRecordsHit()
    {
        byte[] value = [1, 2, 3];
        _inner.Get("settings:tenant-123").Returns(value);

        byte[]? result = _sut.Get("settings:tenant-123");

        result.Should().BeEquivalentTo(value);
        _inner.Received(1).Get("settings:tenant-123");
    }

    [Fact]
    public void Get_WhenKeyMissing_ReturnsNullAndRecordsMiss()
    {
        _inner.Get("missing-key").Returns((byte[]?)null);

        byte[]? result = _sut.Get("missing-key");

        result.Should().BeNull();
        _inner.Received(1).Get("missing-key");
    }

    [Fact]
    public async Task GetAsync_WhenKeyExists_ReturnsValue()
    {
        byte[] value = [10, 20];
        _inner.GetAsync("notifications:data", Arg.Any<CancellationToken>()).Returns(value);

        byte[]? result = await _sut.GetAsync("notifications:data");

        result.Should().BeEquivalentTo(value);
    }

    [Fact]
    public async Task GetAsync_WhenKeyMissing_ReturnsNull()
    {
        _inner.GetAsync("nope", Arg.Any<CancellationToken>()).Returns((byte[]?)null);

        byte[]? result = await _sut.GetAsync("nope");

        result.Should().BeNull();
    }

    [Fact]
    public void Set_DelegatesToInner()
    {
        byte[] value = [1, 2, 3];
        DistributedCacheEntryOptions options = new();

        _sut.Set("key", value, options);

        _inner.Received(1).Set("key", value, options);
    }

    [Fact]
    public async Task SetAsync_DelegatesToInner()
    {
        byte[] value = [1, 2, 3];
        DistributedCacheEntryOptions options = new();

        await _sut.SetAsync("key", value, options);

        await _inner.Received(1).SetAsync("key", value, options, Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Refresh_DelegatesToInner()
    {
        _sut.Refresh("key");

        _inner.Received(1).Refresh("key");
    }

    [Fact]
    public async Task RefreshAsync_DelegatesToInner()
    {
        await _sut.RefreshAsync("key");

        await _inner.Received(1).RefreshAsync("key", Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Remove_DelegatesToInner()
    {
        _sut.Remove("key");

        _inner.Received(1).Remove("key");
    }

    [Fact]
    public async Task RemoveAsync_DelegatesToInner()
    {
        await _sut.RemoveAsync("key");

        await _inner.Received(1).RemoveAsync("key", Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Get_WithKeyContainingColon_ExtractsPrefixUpToFirstColon()
    {
        // Verifies prefix extraction logic handles compound keys like "settings:module:tenantId"
        _inner.Get("settings:module:id").Returns([1]);

        byte[]? result = _sut.Get("settings:module:id");

        result.Should().NotBeNull();
    }

    [Fact]
    public void Get_WithKeyWithoutColon_UsesFullKeyAsPrefix()
    {
        _inner.Get("simplekey").Returns((byte[]?)null);

        byte[]? result = _sut.Get("simplekey");

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAsync_PassesCancellationToken()
    {
        using CancellationTokenSource cts = new();
        CancellationToken ct = cts.Token;
        _inner.GetAsync(Arg.Any<string>(), ct).Returns((byte[]?)null);

        await _sut.GetAsync("key", ct);

        await _inner.Received(1).GetAsync("key", ct);
    }

    [Fact]
    public async Task SetAsync_PassesCancellationToken()
    {
        using CancellationTokenSource cts = new();
        CancellationToken ct = cts.Token;
        DistributedCacheEntryOptions options = new();

        await _sut.SetAsync("key", [1], options, ct);

        await _inner.Received(1).SetAsync("key", Arg.Any<byte[]>(), options, ct);
    }

    [Fact]
    public async Task RemoveAsync_PassesCancellationToken()
    {
        using CancellationTokenSource cts = new();
        CancellationToken ct = cts.Token;

        await _sut.RemoveAsync("key", ct);

        await _inner.Received(1).RemoveAsync("key", ct);
    }
}
