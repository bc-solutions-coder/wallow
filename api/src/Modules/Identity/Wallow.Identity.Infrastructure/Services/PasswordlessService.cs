using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Domain.Errors;
using Wallow.Identity.Infrastructure.Options;
using Wallow.Shared.Contracts.Identity.Events;
using Wallow.Shared.Contracts.RateLimiting;
using Wallow.Shared.Kernel.Errors;
using Wallow.Shared.Kernel.Results;
using Wolverine;

namespace Wallow.Identity.Infrastructure.Services;

public sealed partial class PasswordlessService : IPasswordlessService
{
    private const string ProtectorPurpose = "Wallow.Identity.Passwordless";
    private const string RateLimitKeyPrefix = "pwdless:rate:";
    private const string MagicLinkKeyPrefix = "pwdless:magic:";
    private const string OtpKeyPrefix = "pwdless:otp:";

    private readonly IDatabase _redis;
    private readonly IFixedWindowCounter _counter;
    private readonly IMessageBus _messageBus;
    private readonly UserManager<WallowUser> _userManager;
    private readonly IDataProtector _protector;
    private readonly PasswordlessOptions _options;
    private readonly ILogger<PasswordlessService> _logger;

    public PasswordlessService(
        IConnectionMultiplexer connectionMultiplexer,
        IMessageBus messageBus,
        UserManager<WallowUser> userManager,
        IDataProtectionProvider dataProtectionProvider,
        IOptions<PasswordlessOptions> options,
        ILogger<PasswordlessService> logger,
        IFixedWindowCounter counter)
    {
        _redis = connectionMultiplexer.GetDatabase();
        _counter = counter;
        _messageBus = messageBus;
        _userManager = userManager;
        _options = options.Value;
        _logger = logger;

        // Sign magic-link tokens through Data Protection's shared, persisted key ring
        // (app name "Wallow", persisted to Redis) rather than an extracted key. Deriving a
        // raw key from protector.Protect was non-deterministic, so a token minted in the
        // send request could never be validated in the later verify request (Wallow-gfph).
        _protector = dataProtectionProvider.CreateProtector(ProtectorPurpose);
    }

    public async Task<Result> SendMagicLinkAsync(string email, CancellationToken ct, string? returnUrl = null, string? clientId = null)
    {
        Result throttle = await CheckRateLimitAsync(email);
        if (throttle.IsFailure)
        {
            return throttle;
        }

        WallowUser? user = await _userManager.FindByEmailAsync(email);
        if (user is null)
        {
            // Return success to avoid email enumeration
            LogUserNotFound(email);
            return Result.Success();
        }

        string rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        string signature = _protector.Protect(rawToken);
        string signedToken = $"{rawToken}.{signature}";

        string redisKey = $"{MagicLinkKeyPrefix}{rawToken}";
        await _redis.StringSetAsync(redisKey, email, _options.MagicLinkTtl);

        await _messageBus.PublishAsync(new MagicLinkRequestedEvent
        {
            UserId = user.Id,
            Email = email,
            Token = signedToken,
            ReturnUrl = returnUrl,
            ClientId = clientId
        });

        LogMagicLinkSent(email);
        return Result.Success();
    }

    public async Task<Result<string>> ValidateMagicLinkAsync(string token, CancellationToken ct)
    {
        string[] parts = token.Split('.', 2);
        if (parts.Length != 2)
        {
            return Result.Failure<string>(IdentityErrors.AuthTokenInvalid);
        }

        string rawToken = parts[0];
        string providedSignature = parts[1];

        string unprotectedToken;
        try
        {
            unprotectedToken = _protector.Unprotect(providedSignature);
        }
        catch (CryptographicException)
        {
            LogInvalidSignature();
            return Result.Failure<string>(IdentityErrors.AuthTokenInvalid);
        }
        catch (FormatException)
        {
            LogInvalidSignature();
            return Result.Failure<string>(IdentityErrors.AuthTokenInvalid);
        }

        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(unprotectedToken),
                Encoding.UTF8.GetBytes(rawToken)))
        {
            LogInvalidSignature();
            return Result.Failure<string>(IdentityErrors.AuthTokenInvalid);
        }

        string redisKey = $"{MagicLinkKeyPrefix}{rawToken}";
        string? email = await _redis.StringGetAsync(redisKey);

        if (string.IsNullOrEmpty(email))
        {
            return Result.Failure<string>(IdentityErrors.AuthTokenExpired);
        }

        // Delete token after use (one-time use)
        await _redis.KeyDeleteAsync(redisKey);

        LogMagicLinkValidated(email);
        return email;
    }

    public async Task<Result> SendOtpAsync(string email, CancellationToken ct)
    {
        Result throttle = await CheckRateLimitAsync(email);
        if (throttle.IsFailure)
        {
            return throttle;
        }

        WallowUser? user = await _userManager.FindByEmailAsync(email);
        if (user is null)
        {
            // Return success to avoid email enumeration
            LogUserNotFound(email);
            return Result.Success();
        }

        string code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");

        string redisKey = $"{OtpKeyPrefix}{email}";
        await _redis.StringSetAsync(redisKey, code, _options.OtpTtl);

        await _messageBus.PublishAsync(new OtpCodeRequestedEvent
        {
            UserId = user.Id,
            Email = email,
            Code = code
        });

        LogOtpSent(email);
        return Result.Success();
    }

    public async Task<Result<string>> ValidateOtpAsync(string email, string code, CancellationToken ct)
    {
        string redisKey = $"{OtpKeyPrefix}{email}";
        string? storedCode = await _redis.StringGetAsync(redisKey);

        if (string.IsNullOrEmpty(storedCode))
        {
            return Result.Failure<string>(IdentityErrors.AuthOtpInvalid);
        }

        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(storedCode),
                Encoding.UTF8.GetBytes(code)))
        {
            LogInvalidOtp(email);
            return Result.Failure<string>(IdentityErrors.AuthOtpInvalid);
        }

        // Delete code after use (one-time use)
        await _redis.KeyDeleteAsync(redisKey);

        LogOtpValidated(email);
        return email;
    }

    /// <summary>
    /// Counts the send against the per-address window and, once the window is exhausted,
    /// fails with <see cref="SharedErrors.RateLimitExceeded"/> carrying the time left on it.
    /// </summary>
    private async Task<Result> CheckRateLimitAsync(string email)
    {
        string rateLimitKey = $"{RateLimitKeyPrefix}{email}";
        long count = await _counter.IncrementAsync(rateLimitKey, _options.RateLimitWindow);

        if (count <= _options.RateLimitMaxRequests)
        {
            return Result.Success();
        }

        LogRateLimited(email);
        TimeSpan retryAfter = await _counter.GetRetryAfterAsync(rateLimitKey, _options.RateLimitWindow);
        return Result.Failure(new Error(SharedErrors.RateLimitExceeded, retryAfter: retryAfter));
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Magic link sent to {Email}")]
    private partial void LogMagicLinkSent(string email);

    [LoggerMessage(Level = LogLevel.Information, Message = "Magic link validated for {Email}")]
    private partial void LogMagicLinkValidated(string email);

    [LoggerMessage(Level = LogLevel.Information, Message = "OTP sent to {Email}")]
    private partial void LogOtpSent(string email);

    [LoggerMessage(Level = LogLevel.Information, Message = "OTP validated for {Email}")]
    private partial void LogOtpValidated(string email);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Rate limit exceeded for {Email}")]
    private partial void LogRateLimited(string email);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Magic link token has invalid HMAC signature")]
    private partial void LogInvalidSignature();

    [LoggerMessage(Level = LogLevel.Warning, Message = "Invalid OTP code for {Email}")]
    private partial void LogInvalidOtp(string email);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Passwordless request for non-existent user {Email}, returning success to prevent enumeration")]
    private partial void LogUserNotFound(string email);
}
