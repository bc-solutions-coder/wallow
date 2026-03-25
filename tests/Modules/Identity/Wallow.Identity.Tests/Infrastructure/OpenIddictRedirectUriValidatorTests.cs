using System.Collections.Immutable;
using Wallow.Identity.Infrastructure.Services;
using Wallow.Tests.Common.Fakes;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Configuration;
using OpenIddict.Abstractions;

namespace Wallow.Identity.Tests.Infrastructure;

public sealed class OpenIddictRedirectUriValidatorTests
{
    private readonly IOpenIddictApplicationManager _applicationManager;
    private readonly HybridCache _cache;
    private readonly IConfiguration _configuration;
    private readonly OpenIddictRedirectUriValidator _sut;

    public OpenIddictRedirectUriValidatorTests()
    {
        _applicationManager = Substitute.For<IOpenIddictApplicationManager>();
        _cache = new NoOpHybridCache();
        _configuration = new ConfigurationBuilder().Build();

        SetupEmptyApplicationList();

        _sut = new OpenIddictRedirectUriValidator(_applicationManager, _cache, _configuration);
    }

    [Fact]
    public async Task IsAllowedAsync_NullUri_ReturnsFalse()
    {
        bool result = await _sut.IsAllowedAsync(null!);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsAllowedAsync_EmptyUri_ReturnsFalse()
    {
        bool result = await _sut.IsAllowedAsync("");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsAllowedAsync_WhitespaceUri_ReturnsFalse()
    {
        bool result = await _sut.IsAllowedAsync("   ");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsAllowedAsync_RelativeUri_ReturnsFalse()
    {
        bool result = await _sut.IsAllowedAsync("/callback");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsAllowedAsync_RegisteredRedirectUri_ReturnsTrue()
    {
        object app = new object();
        SetupApplicationList(app);
        _applicationManager.GetRedirectUrisAsync(app, Arg.Any<CancellationToken>())
            .Returns(ImmutableArray.Create("https://myapp.com/callback"));
        _applicationManager.GetPostLogoutRedirectUrisAsync(app, Arg.Any<CancellationToken>())
            .Returns(ImmutableArray<string>.Empty);

        bool result = await _sut.IsAllowedAsync("https://myapp.com/other-path");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsAllowedAsync_RegisteredPostLogoutUri_ReturnsTrue()
    {
        object app = new object();
        SetupApplicationList(app);
        _applicationManager.GetRedirectUrisAsync(app, Arg.Any<CancellationToken>())
            .Returns(ImmutableArray<string>.Empty);
        _applicationManager.GetPostLogoutRedirectUrisAsync(app, Arg.Any<CancellationToken>())
            .Returns(ImmutableArray.Create("https://myapp.com/logout"));

        bool result = await _sut.IsAllowedAsync("https://myapp.com/any-path");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsAllowedAsync_UnregisteredOrigin_ReturnsFalse()
    {
        object app = new object();
        SetupApplicationList(app);
        _applicationManager.GetRedirectUrisAsync(app, Arg.Any<CancellationToken>())
            .Returns(ImmutableArray.Create("https://myapp.com/callback"));
        _applicationManager.GetPostLogoutRedirectUrisAsync(app, Arg.Any<CancellationToken>())
            .Returns(ImmutableArray<string>.Empty);

        bool result = await _sut.IsAllowedAsync("https://evil.com/callback");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsAllowedAsync_LocalhostWithPort_MatchesOriginWithPort()
    {
        object app = new object();
        SetupApplicationList(app);
        _applicationManager.GetRedirectUrisAsync(app, Arg.Any<CancellationToken>())
            .Returns(ImmutableArray.Create("http://localhost:3000/callback"));
        _applicationManager.GetPostLogoutRedirectUrisAsync(app, Arg.Any<CancellationToken>())
            .Returns(ImmutableArray<string>.Empty);

        bool result = await _sut.IsAllowedAsync("http://localhost:3000/other");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsAllowedAsync_LocalhostDifferentPort_ReturnsFalse()
    {
        object app = new object();
        SetupApplicationList(app);
        _applicationManager.GetRedirectUrisAsync(app, Arg.Any<CancellationToken>())
            .Returns(ImmutableArray.Create("http://localhost:3000/callback"));
        _applicationManager.GetPostLogoutRedirectUrisAsync(app, Arg.Any<CancellationToken>())
            .Returns(ImmutableArray<string>.Empty);

        bool result = await _sut.IsAllowedAsync("http://localhost:4000/callback");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsAllowedAsync_OriginMatchIsCaseInsensitive()
    {
        object app = new object();
        SetupApplicationList(app);
        _applicationManager.GetRedirectUrisAsync(app, Arg.Any<CancellationToken>())
            .Returns(ImmutableArray.Create("https://MyApp.COM/callback"));
        _applicationManager.GetPostLogoutRedirectUrisAsync(app, Arg.Any<CancellationToken>())
            .Returns(ImmutableArray<string>.Empty);

        bool result = await _sut.IsAllowedAsync("https://myapp.com/other");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsAllowedAsync_AuthUrlFromConfiguration_IsAllowed()
    {
        Dictionary<string, string?> configData = new() { { "AuthUrl", "https://auth.example.com" } };
        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();
        OpenIddictRedirectUriValidator sut = new(_applicationManager, new NoOpHybridCache(), config);

        bool result = await sut.IsAllowedAsync("https://auth.example.com/login");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsAllowedAsync_MultipleCallsWithNoOpCache_QueriesManagerEachTime()
    {
        object app = new object();
        SetupApplicationList(app);
        _applicationManager.GetRedirectUrisAsync(app, Arg.Any<CancellationToken>())
            .Returns(ImmutableArray.Create("https://myapp.com/callback"));
        _applicationManager.GetPostLogoutRedirectUrisAsync(app, Arg.Any<CancellationToken>())
            .Returns(ImmutableArray<string>.Empty);

        await _sut.IsAllowedAsync("https://myapp.com/first");
        await _sut.IsAllowedAsync("https://myapp.com/second");

        // NoOpHybridCache does not cache, so the factory is invoked on every call
        await _applicationManager.Received(2).GetRedirectUrisAsync(app, Arg.Any<CancellationToken>());
    }

    private void SetupEmptyApplicationList()
    {
        _applicationManager.ListAsync(Arg.Any<int?>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(ToAsyncEnumerable(Array.Empty<object>()));
    }

    private void SetupApplicationList(params object[] apps)
    {
        _applicationManager.ListAsync(Arg.Any<int?>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(ToAsyncEnumerable(apps));
    }

    private static async IAsyncEnumerable<object> ToAsyncEnumerable(object[] items)
    {
        foreach (object item in items)
        {
            yield return item;
        }

        await Task.CompletedTask;
    }
}
