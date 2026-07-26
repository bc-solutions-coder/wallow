using System.Collections.Immutable;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Configuration;
using OpenIddict.Abstractions;
using Wallow.Identity.Application.Interfaces;

namespace Wallow.Identity.Infrastructure.Services;

public sealed class OpenIddictRedirectUriValidator(
    IOpenIddictApplicationManager applicationManager,
    HybridCache cache,
    IConfiguration configuration) : IRedirectUriValidator
{
    private const string CacheKeyPrefix = "allowed_redirect_origins:";

    // Cache slot used when no client_id is known; holds the union of every registered client's origins.
    private const string AllClientsCacheKey = $"{CacheKeyPrefix}*";

    private static readonly HybridCacheEntryOptions _cacheOptions = new()
    {
        Expiration = TimeSpan.FromMinutes(5),
        LocalCacheExpiration = TimeSpan.FromMinutes(2)
    };

    public async Task<bool> IsAllowedAsync(string uri, string? clientId = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(uri) || !Uri.TryCreate(uri, UriKind.Absolute, out Uri? parsed))
        {
            return false;
        }

        string origin = GetOrigin(parsed);
        HashSet<string> allowedOrigins = await GetAllowedOriginsAsync(clientId, ct);
        return allowedOrigins.Contains(origin);
    }

    private async Task<HashSet<string>> GetAllowedOriginsAsync(string? clientId, CancellationToken ct)
    {
        bool scopedToClient = !string.IsNullOrWhiteSpace(clientId);
        string cacheKey = scopedToClient ? CacheKeyPrefix + clientId : AllClientsCacheKey;

        return await cache.GetOrCreateAsync(cacheKey, async token =>
        {
            HashSet<string> origins = new(StringComparer.OrdinalIgnoreCase);

            if (scopedToClient)
            {
                // An unknown client resolves to no application, leaving only the AuthUrl origin below.
                object? application = await applicationManager.FindByClientIdAsync(clientId!, token);
                if (application is not null)
                {
                    await AddApplicationOriginsAsync(application, origins, token);
                }
            }
            else
            {
                await foreach (object app in applicationManager.ListAsync(null, null, token))
                {
                    await AddApplicationOriginsAsync(app, origins, token);
                }
            }

            string? authUrl = configuration["AuthUrl"];
            if (!string.IsNullOrEmpty(authUrl) && Uri.TryCreate(authUrl, UriKind.Absolute, out Uri? authUri))
            {
                origins.Add(GetOrigin(authUri));
            }

            return origins;
        }, _cacheOptions, cancellationToken: ct);
    }

    private async Task AddApplicationOriginsAsync(object application, HashSet<string> origins, CancellationToken ct)
    {
        ImmutableArray<string> redirectUris = await applicationManager.GetRedirectUrisAsync(application, ct);
        AddOrigins(redirectUris, origins);

        ImmutableArray<string> postLogoutUris = await applicationManager.GetPostLogoutRedirectUrisAsync(application, ct);
        AddOrigins(postLogoutUris, origins);
    }

    private static void AddOrigins(ImmutableArray<string> uris, HashSet<string> origins)
    {
        foreach (string uri in uris)
        {
            if (Uri.TryCreate(uri, UriKind.Absolute, out Uri? parsedUri))
            {
                origins.Add(GetOrigin(parsedUri));
            }
        }
    }

    private static string GetOrigin(Uri uri) =>
        uri.IsDefaultPort ? $"{uri.Scheme}://{uri.Host}" : $"{uri.Scheme}://{uri.Host}:{uri.Port}";
}
