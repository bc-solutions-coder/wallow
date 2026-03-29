using Microsoft.Extensions.Caching.Memory;
using Wallow.Branding.Application.DTOs;
using Wallow.Branding.Application.Interfaces;
using Wallow.Branding.Domain.Entities;
using Wallow.Branding.Infrastructure.Services;
using Wallow.Shared.Contracts.Storage;

namespace Wallow.Branding.Tests.Infrastructure;

public sealed class ClientBrandingServiceTests : IDisposable
{
    private readonly IClientBrandingRepository _repository;
    private readonly IStorageProvider _storageProvider;
    private readonly MemoryCache _cache;
    private readonly ClientBrandingService _sut;

    public ClientBrandingServiceTests()
    {
        _repository = Substitute.For<IClientBrandingRepository>();
        _storageProvider = Substitute.For<IStorageProvider>();
        _cache = new MemoryCache(new MemoryCacheOptions());
        _sut = new ClientBrandingService(_repository, _storageProvider, _cache);
    }

    public void Dispose()
    {
        _cache.Dispose();
    }

    [Fact]
    public async Task GetBrandingAsync_WhenEntityNotFound_ReturnsNull()
    {
        _repository.GetByClientIdAsync("unknown", Arg.Any<CancellationToken>())
            .Returns((ClientBranding?)null);

        ClientBrandingDto? result = await _sut.GetBrandingAsync("unknown");

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetBrandingAsync_WhenEntityExists_ReturnsDto()
    {
        ClientBranding entity = ClientBranding.Create("client-1", "My App", "Tagline");
        _repository.GetByClientIdAsync("client-1", Arg.Any<CancellationToken>())
            .Returns(entity);

        ClientBrandingDto? result = await _sut.GetBrandingAsync("client-1");

        result.Should().NotBeNull();
        result!.ClientId.Should().Be("client-1");
        result.DisplayName.Should().Be("My App");
        result.Tagline.Should().Be("Tagline");
        result.LogoUrl.Should().BeNull();
    }

    [Fact]
    public async Task GetBrandingAsync_WhenEntityHasLogo_GeneratesPresignedUrl()
    {
        ClientBranding entity = ClientBranding.Create("client-1", "My App", logoStorageKey: "logos/key.png");
        _repository.GetByClientIdAsync("client-1", Arg.Any<CancellationToken>())
            .Returns(entity);
        _storageProvider.GetPresignedUrlAsync("logos/key.png", Arg.Any<TimeSpan>(), false, Arg.Any<CancellationToken>())
            .Returns("https://cdn.example.com/logos/key.png?signed");

        ClientBrandingDto? result = await _sut.GetBrandingAsync("client-1");

        result.Should().NotBeNull();
        result!.LogoUrl.Should().Be("https://cdn.example.com/logos/key.png?signed");
    }

    [Fact]
    public async Task GetBrandingAsync_CachesResultOnSecondCall()
    {
        ClientBranding entity = ClientBranding.Create("client-1", "My App");
        _repository.GetByClientIdAsync("client-1", Arg.Any<CancellationToken>())
            .Returns(entity);

        await _sut.GetBrandingAsync("client-1");
        await _sut.GetBrandingAsync("client-1");

        await _repository.Received(1).GetByClientIdAsync("client-1", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetBrandingAsync_WhenEntityNotFound_CachesNullResult()
    {
        _repository.GetByClientIdAsync("missing", Arg.Any<CancellationToken>())
            .Returns((ClientBranding?)null);

        await _sut.GetBrandingAsync("missing");
        await _sut.GetBrandingAsync("missing");

        await _repository.Received(1).GetByClientIdAsync("missing", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetBrandingAsync_WhenEntityHasThemeJson_ReturnsDtoWithTheme()
    {
        ClientBranding entity = ClientBranding.Create("client-1", "My App", themeJson: "{\"primary\":\"#000\"}");
        _repository.GetByClientIdAsync("client-1", Arg.Any<CancellationToken>())
            .Returns(entity);

        ClientBrandingDto? result = await _sut.GetBrandingAsync("client-1");

        result.Should().NotBeNull();
        result!.ThemeJson.Should().Be("{\"primary\":\"#000\"}");
    }

    [Fact]
    public async Task InvalidateCache_ForcesRefreshOnNextCall()
    {
        ClientBranding entity = ClientBranding.Create("client-1", "My App");
        _repository.GetByClientIdAsync("client-1", Arg.Any<CancellationToken>())
            .Returns(entity);

        await _sut.GetBrandingAsync("client-1");
        _sut.InvalidateCache("client-1");
        await _sut.GetBrandingAsync("client-1");

        await _repository.Received(2).GetByClientIdAsync("client-1", Arg.Any<CancellationToken>());
    }
}
