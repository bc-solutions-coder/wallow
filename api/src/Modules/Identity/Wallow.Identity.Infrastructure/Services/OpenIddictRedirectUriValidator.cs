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
    private const string CacheKey = "allowed_redirect_origins";

    private static readonly HybridCacheEntryOptions _cacheOptions = new()
    {
        Expiration = TimeSpan.FromMinutes(5),
        LocalCacheExpiration = TimeSpan.FromMinutes(2)
    };

    public async Task<bool> IsAllowedAsync(string uri, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(uri) || !Uri.TryCreate(uri, UriKind.Absolute, out Uri? parsed))
        {
            return false;
        }

        string origin = GetOrigin(parsed);
        HashSet<string> allowedOrigins = await GetAllowedOriginsAsync(ct);
        return allowedOrigins.Contains(origin);
    }

    private async Task<HashSet<string>> GetAllowedOriginsAsync(CancellationToken ct)
    {
        return await cache.GetOrCreateAsync(CacheKey, async _ =>
        {
            HashSet<string> origins = new(StringComparer.OrdinalIgnoreCase);

            await foreach (object app in applicationManager.ListAsync(null, null, ct))
            {
                ImmutableArray<string> redirectUris = await applicationManager.GetRedirectUrisAsync(app, ct);
                foreach (string redirectUri in redirectUris)
                {
                    if (Uri.TryCreate(redirectUri, UriKind.Absolute, out Uri? parsedUri))
                    {
                        origins.Add(GetOrigin(parsedUri));
                    }
                }

                ImmutableArray<string> postLogoutUris = await applicationManager.GetPostLogoutRedirectUrisAsync(app, ct);
                foreach (string postLogoutUri in postLogoutUris)
                {
                    if (Uri.TryCreate(postLogoutUri, UriKind.Absolute, out Uri? parsedUri))
                    {
                        origins.Add(GetOrigin(parsedUri));
                    }
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

    private static string GetOrigin(Uri uri) =>
        uri.IsDefaultPort ? $"{uri.Scheme}://{uri.Host}" : $"{uri.Scheme}://{uri.Host}:{uri.Port}";
}
