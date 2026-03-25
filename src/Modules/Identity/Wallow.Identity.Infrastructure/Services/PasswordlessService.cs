using System.Security.Cryptography;
using System.Text;

using Wallow.Identity.Application.DTOs;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Infrastructure.Options;
using Wallow.Shared.Contracts.Identity.Events;
using Wallow.Shared.Kernel.MultiTenancy;

using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using StackExchange.Redis;

using Wolverine;

namespace Wallow.Identity.Infrastructure.Services;

public sealed partial class PasswordlessService : IPasswordlessService
{
    private const string ProtectorPurpose = "Wallow.Identity.Passwordless";
    private const string RateLimitKeyPrefix = "pwdless:rate:";
    private const string MagicLinkKeyPrefix = "pwdless:magic:";
    private const string OtpKeyPrefix = "pwdless:otp:";

    private readonly IDatabase _redis;
    private readonly IMessageBus _messageBus;
    private readonly UserManager<WallowUser> _userManager;
    private readonly ITenantContext _tenantContext;
    private readonly byte[] _hmacKey;
    private readonly PasswordlessOptions _options;
    private readonly ILogger<PasswordlessService> _logger;

    public PasswordlessService(
        IConnectionMultiplexer connectionMultiplexer,
        IMessageBus messageBus,
        UserManager<WallowUser> userManager,
        ITenantContext tenantContext,
        IDataProtectionProvider dataProtectionProvider,
        IOptions<PasswordlessOptions> options,
        ILogger<PasswordlessService> logger)
    {
        _redis = connectionMultiplexer.GetDatabase();
        _messageBus = messageBus;
        _userManager = userManager;
        _tenantContext = tenantContext;
        _options = options.Value;
        _logger = logger;

        // Derive a stable HMAC key from Data Protection
        IDataProtector protector = dataProtectionProvider.CreateProtector(ProtectorPurpose);
        _hmacKey = protector.Protect(Encoding.UTF8.GetBytes(ProtectorPurpose));
    }

    public async Task<PasswordlessResult> SendMagicLinkAsync(string email, CancellationToken ct)
    {
        if (!await IsWithinRateLimitAsync(email))
        {
            LogRateLimited(email);
            return new PasswordlessResult(false, email, "Rate limit exceeded. Please try again later.");
        }

        WallowUser? user = await _userManager.FindByEmailAsync(email);
        if (user is null)
        {
            // Return success to avoid email enumeration
            LogUserNotFound(email);
            return new PasswordlessResult(true, email, null);
        }

        string rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        string signature = ComputeHmac(rawToken);
        string signedToken = $"{rawToken}.{signature}";

        string redisKey = $"{MagicLinkKeyPrefix}{rawToken}";
        await _redis.StringSetAsync(redisKey, email, _options.MagicLinkTtl);

        await _messageBus.PublishAsync(new MagicLinkRequestedEvent
        {
            UserId = user.Id,
            TenantId = _tenantContext.TenantId.Value,
            Email = email,
            Token = signedToken
        });

        LogMagicLinkSent(email);
        return new PasswordlessResult(true, email, null);
    }

    public async Task<PasswordlessResult> ValidateMagicLinkAsync(string token, CancellationToken ct)
    {
        string[] parts = token.Split('.', 2);
        if (parts.Length != 2)
        {
            return new PasswordlessResult(false, null, "Invalid token format.");
        }

        string rawToken = parts[0];
        string providedSignature = parts[1];

        string expectedSignature = ComputeHmac(rawToken);
        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(expectedSignature),
                Encoding.UTF8.GetBytes(providedSignature)))
        {
            LogInvalidSignature();
            return new PasswordlessResult(false, null, "Invalid token.");
        }

        string redisKey = $"{MagicLinkKeyPrefix}{rawToken}";
        string? email = await _redis.StringGetAsync(redisKey);

        if (string.IsNullOrEmpty(email))
        {
            return new PasswordlessResult(false, null, "Token expired or already used.");
        }

        // Delete token after use (one-time use)
        await _redis.KeyDeleteAsync(redisKey);

        LogMagicLinkValidated(email);
        return new PasswordlessResult(true, email, null);
    }

    public async Task<PasswordlessResult> SendOtpAsync(string email, CancellationToken ct)
    {
        if (!await IsWithinRateLimitAsync(email))
        {
            LogRateLimited(email);
            return new PasswordlessResult(false, email, "Rate limit exceeded. Please try again later.");
        }

        WallowUser? user = await _userManager.FindByEmailAsync(email);
        if (user is null)
        {
            // Return success to avoid email enumeration
            LogUserNotFound(email);
            return new PasswordlessResult(true, email, null);
        }

        string code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");

        string redisKey = $"{OtpKeyPrefix}{email}";
        await _redis.StringSetAsync(redisKey, code, _options.OtpTtl);

        await _messageBus.PublishAsync(new OtpCodeRequestedEvent
        {
            UserId = user.Id,
            TenantId = _tenantContext.TenantId.Value,
            Email = email,
            Code = code
        });

        LogOtpSent(email);
        return new PasswordlessResult(true, email, null);
    }

    public async Task<PasswordlessResult> ValidateOtpAsync(string email, string code, CancellationToken ct)
    {
        string redisKey = $"{OtpKeyPrefix}{email}";
        string? storedCode = await _redis.StringGetAsync(redisKey);

        if (string.IsNullOrEmpty(storedCode))
        {
            return new PasswordlessResult(false, email, "Code expired or not found.");
        }

        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(storedCode),
                Encoding.UTF8.GetBytes(code)))
        {
            LogInvalidOtp(email);
            return new PasswordlessResult(false, email, "Invalid code.");
        }

        // Delete code after use (one-time use)
        await _redis.KeyDeleteAsync(redisKey);

        LogOtpValidated(email);
        return new PasswordlessResult(true, email, null);
    }

    private async Task<bool> IsWithinRateLimitAsync(string email)
    {
        string rateLimitKey = $"{RateLimitKeyPrefix}{email}";
        long count = await _redis.StringIncrementAsync(rateLimitKey);

        if (count == 1)
        {
            await _redis.KeyExpireAsync(rateLimitKey, _options.RateLimitWindow);
        }

        return count <= _options.RateLimitMaxRequests;
    }

    private string ComputeHmac(string data)
    {
        byte[] hash = HMACSHA256.HashData(_hmacKey, Encoding.UTF8.GetBytes(data));
        return Convert.ToBase64String(hash);
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
