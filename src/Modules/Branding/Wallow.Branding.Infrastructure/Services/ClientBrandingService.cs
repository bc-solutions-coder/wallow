using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Wallow.Branding.Application.DTOs;
using Wallow.Branding.Application.Interfaces;
using Wallow.Branding.Domain.Entities;
using Wallow.Shared.Contracts.Storage;

namespace Wallow.Branding.Infrastructure.Services;

public sealed class ClientBrandingService : IClientBrandingService
{
    private static readonly TimeSpan _logoUrlExpiry = TimeSpan.FromHours(1);
    private static readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(5);

    private readonly IClientBrandingRepository _repository;
    private readonly IStorageProvider _storageProvider;
    private readonly IMemoryCache _cache;

    public ClientBrandingService(
        IClientBrandingRepository repository,
        IStorageProvider storageProvider,
        [FromKeyedServices("BrandingCache")] IMemoryCache cache)
    {
        _repository = repository;
        _storageProvider = storageProvider;
        _cache = cache;
    }

    public async Task<ClientBrandingDto?> GetBrandingAsync(string clientId, CancellationToken ct = default)
    {
        string cacheKey = $"client-branding:{clientId}";

        if (_cache.TryGetValue(cacheKey, out ClientBrandingDto? cached))
        {
            return cached;
        }

        ClientBranding? entity = await _repository.GetByClientIdAsync(clientId, ct);
        if (entity is null)
        {
            _cache.Set(cacheKey, (ClientBrandingDto?)null, new MemoryCacheEntryOptions
            {
                SlidingExpiration = _cacheDuration,
                Size = 1
            });
            return null;
        }

        string? logoUrl = null;
        if (!string.IsNullOrEmpty(entity.LogoStorageKey))
        {
            logoUrl = await _storageProvider.GetPresignedUrlAsync(
                entity.LogoStorageKey, _logoUrlExpiry, forUpload: false, ct: ct);
        }

        ClientBrandingDto dto = new(
            entity.ClientId,
            entity.DisplayName,
            entity.Tagline,
            logoUrl,
            entity.ThemeJson);

        _cache.Set(cacheKey, (ClientBrandingDto?)dto, new MemoryCacheEntryOptions
        {
            SlidingExpiration = _cacheDuration,
            Size = 1
        });

        return dto;
    }

    public void InvalidateCache(string clientId)
    {
        _cache.Remove($"client-branding:{clientId}");
    }
}
