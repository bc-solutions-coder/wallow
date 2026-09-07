using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using Wallow.Identity.Application.Interfaces;

namespace Wallow.Identity.Infrastructure.Services;

/// <summary>
/// Protects user/request-bound consent tokens with an expiry and records redeemed IDs
/// in HybridCache. Replay detection depends on retention and coordination of cache entries.
/// </summary>
public sealed partial class ConsentTokenService : IConsentTokenService
{
    /// <summary>
    /// Validity window for a consent decision token.
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

        // Only the call that runs the cache factory is accepted as a new redemption.
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
            // Invalid protection or payload formats all produce the same rejection outcome.
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
