using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using Wallow.Identity.Application.Interfaces;

namespace Wallow.Identity.Infrastructure.Services;

/// <summary>
/// Consent tokens are data-protected, time-limited payloads naming the user and the request
/// fingerprint they were minted for, plus an id the redemption records in the cache so the token is
/// good exactly once. The cache carries the redeemed ids for as long as a token could still be
/// valid, after which the time limit refuses it on its own. The data protector is the same
/// mechanism the MFA partial-auth cookie and ticket exchange rely on, so a token is unforgeable
/// without the key ring and readable by every host sharing it.
/// </summary>
public sealed partial class ConsentTokenService : IConsentTokenService
{
    /// <summary>
    /// How long a consent screen may sit before its decision is refused. Long enough to read the
    /// scope list; short enough that a leaked link is not a standing invitation.
    /// </summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);

    private const string ProtectorPurpose = "Wallow.Identity.ConsentToken";
    private const string RedeemedKeyPrefix = "consent_token_redeemed:";

    private static readonly HybridCacheEntryOptions _redeemedOptions = new()
    {
        Expiration = Lifetime,
        LocalCacheExpiration = Lifetime,
    };

    private readonly ITimeLimitedDataProtector _protector;
    private readonly HybridCache _cache;
    private readonly ILogger<ConsentTokenService> _logger;

    public ConsentTokenService(
        IDataProtectionProvider dataProtectionProvider,
        HybridCache cache,
        ILogger<ConsentTokenService> logger)
    {
        ArgumentNullException.ThrowIfNull(dataProtectionProvider);

        _protector = dataProtectionProvider.CreateProtector(ProtectorPurpose).ToTimeLimitedDataProtector();
        _cache = cache;
        _logger = logger;
    }

    public string Issue(string userId, string requestFingerprint)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);
        ArgumentException.ThrowIfNullOrEmpty(requestFingerprint);

        Payload payload = new(Guid.NewGuid().ToString("N"), userId, requestFingerprint);
        return _protector.Protect(JsonSerializer.Serialize(payload), Lifetime);
    }

    public async ValueTask<ConsentTokenOutcome> RedeemAsync(
        string? token,
        string userId,
        string requestFingerprint,
        CancellationToken ct)
    {
        if (string.IsNullOrEmpty(token))
        {
            return ConsentTokenOutcome.Missing;
        }

        Payload? payload = Unprotect(token);
        if (payload is null)
        {
            return ConsentTokenOutcome.Invalid;
        }

        if (!string.Equals(payload.UserId, userId, StringComparison.Ordinal)
            || !string.Equals(payload.RequestFingerprint, requestFingerprint, StringComparison.Ordinal))
        {
            LogMismatched(userId);
            return ConsentTokenOutcome.Mismatched;
        }

        // First redemption creates the entry; every later one finds it. The factory runs only
        // when the key is absent, which is what makes the redemption single-use.
        bool redeemedNow = false;
        await _cache.GetOrCreateAsync(
            RedeemedKeyPrefix + payload.Id,
            _ =>
            {
                redeemedNow = true;
                return ValueTask.FromResult(true);
            },
            _redeemedOptions,
            cancellationToken: ct);

        if (!redeemedNow)
        {
            LogReplayed(userId);
            return ConsentTokenOutcome.Replayed;
        }

        return ConsentTokenOutcome.Redeemed;
    }

    private Payload? Unprotect(string token)
    {
        try
        {
            return JsonSerializer.Deserialize<Payload>(_protector.Unprotect(token));
        }
        catch (Exception e) when (e is CryptographicException or FormatException or JsonException)
        {
            // Forged, tampered, expired, protected under a key this host does not hold, or not
            // a token at all - every one of them is "not ours", never an error to surface.
            LogInvalid();
            return null;
        }
    }

    private sealed record Payload(string Id, string UserId, string RequestFingerprint);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Consent token refused: not issued by this server, or expired")]
    private partial void LogInvalid();

    [LoggerMessage(Level = LogLevel.Warning, Message = "Consent token refused for user {UserId}: minted for another user or request")]
    private partial void LogMismatched(string userId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Consent token refused for user {UserId}: already redeemed")]
    private partial void LogReplayed(string userId);
}
