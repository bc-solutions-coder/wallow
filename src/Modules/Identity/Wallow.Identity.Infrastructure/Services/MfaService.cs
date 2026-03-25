using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Entities;
using Wallow.Shared.Kernel.Results;

namespace Wallow.Identity.Infrastructure.Services;

public sealed partial class MfaService : IMfaService
{
    private const string ProtectorPurpose = "Wallow.Identity.Mfa";
    private const string ChallengeKeyPrefix = "mfa:challenge:";
    private const string FailureKeyPrefix = "mfa:failures:";
    private const int TotpDigits = 6;
    private const int TotpStepSeconds = 30;
    private const int BackupCodeCount = 10;
    private const int BackupCodeLength = 8;

    private static readonly TimeSpan _challengeTtl = TimeSpan.FromMinutes(5);

    private readonly IDataProtector _protector;
    private readonly IDistributedCache _cache;
    private readonly IDatabase _redis;
    private readonly UserManager<WallowUser> _userManager;
    private readonly ILogger<MfaService> _logger;
    private readonly int _maxFailedAttempts;

    public MfaService(
        IDataProtectionProvider dataProtectionProvider,
        IDistributedCache cache,
        IConnectionMultiplexer connectionMultiplexer,
        UserManager<WallowUser> userManager,
        IConfiguration configuration,
        ILogger<MfaService> logger)
    {
        _protector = dataProtectionProvider.CreateProtector(ProtectorPurpose);
        _cache = cache;
        _redis = connectionMultiplexer.GetDatabase();
        _userManager = userManager;
        _logger = logger;
        _maxFailedAttempts = configuration.GetValue("Mfa:MaxFailedAttempts", 5);
    }

    public Task<(string Secret, string QrUri)> GenerateEnrollmentSecretAsync(string userId, CancellationToken ct)
    {
        byte[] secretBytes = RandomNumberGenerator.GetBytes(20);
        string base32Secret = ToBase32(secretBytes);

        string protectedSecret = _protector.Protect(base32Secret);

        string qrUri = $"otpauth://totp/Wallow:{userId}?secret={base32Secret}&issuer=Wallow&digits={TotpDigits}&period={TotpStepSeconds}";

        LogEnrollmentSecretGenerated(userId);
        return Task.FromResult((protectedSecret, qrUri));
    }

    public Task<bool> ValidateTotpAsync(string secret, string code, CancellationToken ct)
    {
        string base32Secret = _protector.Unprotect(secret);
        byte[] secretBytes = FromBase32(base32Secret);

        long currentStep = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / TotpStepSeconds;

        // Allow a 1-step window in each direction to account for clock drift
        for (long step = currentStep - 1; step <= currentStep + 1; step++)
        {
            string expectedCode = ComputeTotp(secretBytes, step);
            if (CryptographicOperations.FixedTimeEquals(
                    Encoding.UTF8.GetBytes(expectedCode),
                    Encoding.UTF8.GetBytes(code)))
            {
                return Task.FromResult(true);
            }
        }

        return Task.FromResult(false);
    }

    public async Task<string> IssueChallengeAsync(string userId, CancellationToken ct)
    {
        string challengeToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

        DistributedCacheEntryOptions options = new()
        {
            AbsoluteExpirationRelativeToNow = _challengeTtl
        };

        await _cache.SetStringAsync($"{ChallengeKeyPrefix}{userId}", challengeToken, options, ct);

        // Reset failure count on new challenge
        await _cache.RemoveAsync($"{FailureKeyPrefix}{userId}", ct);

        LogChallengeIssued(userId);
        return challengeToken;
    }

    public async Task<Result> ValidateChallengeAsync(string userId, string code, CancellationToken ct)
    {
        // Check lockout
        string? failureCountStr = await _cache.GetStringAsync($"{FailureKeyPrefix}{userId}", ct);
        int failureCount = int.TryParse(failureCountStr, out int parsed) ? parsed : 0;

        if (failureCount >= _maxFailedAttempts)
        {
            LogChallengeLocked(userId, failureCount);
            return Result.Failure(Error.Validation("Mfa.Locked", "Too many failed MFA attempts. Please request a new challenge."));
        }

        string? storedToken = await _cache.GetStringAsync($"{ChallengeKeyPrefix}{userId}", ct);

        if (string.IsNullOrEmpty(storedToken))
        {
            return Result.Failure(Error.Validation("Mfa.ExpiredChallenge", "MFA challenge has expired. Please request a new one."));
        }

        bool isValid = CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(storedToken),
            Encoding.UTF8.GetBytes(code));

        if (!isValid)
        {
            int newCount = failureCount + 1;
            DistributedCacheEntryOptions failureOptions = new()
            {
                AbsoluteExpirationRelativeToNow = _challengeTtl
            };
            await _cache.SetStringAsync($"{FailureKeyPrefix}{userId}", newCount.ToString(), failureOptions, ct);

            LogChallengeValidationFailed(userId, newCount);
            return Result.Failure(Error.Validation("Mfa.InvalidCode", "Invalid MFA code."));
        }

        // Clean up on success
        await _cache.RemoveAsync($"{ChallengeKeyPrefix}{userId}", ct);
        await _cache.RemoveAsync($"{FailureKeyPrefix}{userId}", ct);

        LogChallengeValidated(userId);
        return Result.Success();
    }

    public Task<List<string>> GenerateBackupCodesAsync(CancellationToken ct)
    {
        List<string> codes = new(BackupCodeCount);
        for (int i = 0; i < BackupCodeCount; i++)
        {
            byte[] bytes = RandomNumberGenerator.GetBytes(BackupCodeLength);
            // Format as hex pairs separated by dashes for readability (e.g., "a1b2-c3d4-e5f6-g7h8")
            string hex = Convert.ToHexStringLower(bytes);
            string formatted = $"{hex[..4]}-{hex[4..8]}-{hex[8..12]}-{hex[12..16]}";
            codes.Add(formatted);
        }

        return Task.FromResult(codes);
    }

    public async Task<string> IssueMfaChallengeTokenAsync(string userId, CancellationToken ct)
    {
        string token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        string redisKey = $"{ChallengeKeyPrefix}{userId}:{token}";

        await _redis.StringSetAsync(redisKey, "1", _challengeTtl);

        LogChallengeIssued(userId);
        return token;
    }

    public async Task<bool> ValidateBackupCodeAsync(string userId, string code, CancellationToken ct)
    {
        WallowUser? user = await _userManager.FindByIdAsync(userId);
        if (user is null || string.IsNullOrEmpty(user.BackupCodesHash))
        {
            return false;
        }

        string codeHash = ComputeBackupCodeHash(code);

        List<string>? storedHashes = JsonSerializer.Deserialize<List<string>>(user.BackupCodesHash);
        if (storedHashes is null || !storedHashes.Remove(codeHash))
        {
            return false;
        }

        // Mark the code as consumed by saving the remaining codes
        string updatedHash = JsonSerializer.Serialize(storedHashes);
        user.SetBackupCodes(updatedHash);
        await _userManager.UpdateAsync(user);

        LogBackupCodeUsed(userId);
        return true;
    }

    public async Task<bool> ValidateChallengeAsync(string userId, string challengeToken, string code, CancellationToken ct)
    {
        string redisKey = $"{ChallengeKeyPrefix}{userId}:{challengeToken}";

        // Verify the challenge token exists in Redis
        bool challengeExists = await _redis.KeyExistsAsync(redisKey);
        if (!challengeExists)
        {
            return false;
        }

        // Validate the TOTP code against the user's stored secret
        WallowUser? user = await _userManager.FindByIdAsync(userId);
        if (user is null || string.IsNullOrEmpty(user.TotpSecretEncrypted))
        {
            return false;
        }

        bool isValid = await ValidateTotpAsync(user.TotpSecretEncrypted, code, ct);
        if (!isValid)
        {
            return false;
        }

        // Delete the challenge token after successful validation
        await _redis.KeyDeleteAsync(redisKey);

        LogChallengeValidated(userId);
        return true;
    }

    private static string ComputeBackupCodeHash(string code)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(code));
        return Convert.ToHexStringLower(hash);
    }

    private static string ComputeTotp(byte[] secret, long step)
    {
        byte[] stepBytes = BitConverter.GetBytes(step);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(stepBytes);
        }

        // RFC 6238 mandates HMAC-SHA1 for TOTP interoperability with authenticator apps
#pragma warning disable CA5350
        byte[] hash = HMACSHA1.HashData(secret, stepBytes);
#pragma warning restore CA5350

        int offset = hash[^1] & 0x0F;
        int binaryCode = ((hash[offset] & 0x7F) << 24)
                         | ((hash[offset + 1] & 0xFF) << 16)
                         | ((hash[offset + 2] & 0xFF) << 8)
                         | (hash[offset + 3] & 0xFF);

        int otp = binaryCode % (int)Math.Pow(10, TotpDigits);
        return otp.ToString().PadLeft(TotpDigits, '0');
    }

    private static string ToBase32(byte[] data)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        StringBuilder result = new((data.Length * 8 + 4) / 5);

        int buffer = 0;
        int bitsLeft = 0;

        foreach (byte b in data)
        {
            buffer = (buffer << 8) | b;
            bitsLeft += 8;

            while (bitsLeft >= 5)
            {
                bitsLeft -= 5;
                result.Append(alphabet[(buffer >> bitsLeft) & 0x1F]);
            }
        }

        if (bitsLeft > 0)
        {
            result.Append(alphabet[(buffer << (5 - bitsLeft)) & 0x1F]);
        }

        return result.ToString();
    }

    private static byte[] FromBase32(string base32)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        string input = base32.TrimEnd('=').ToUpperInvariant();

        List<byte> output = new(input.Length * 5 / 8);
        int buffer = 0;
        int bitsLeft = 0;

        foreach (char c in input)
        {
            int value = alphabet.IndexOf(c, StringComparison.Ordinal);
            if (value < 0)
            {
                throw new FormatException($"Invalid base32 character: {c}");
            }

            buffer = (buffer << 5) | value;
            bitsLeft += 5;

            if (bitsLeft >= 8)
            {
                bitsLeft -= 8;
                output.Add((byte)(buffer >> bitsLeft));
            }
        }

        return output.ToArray();
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Generated MFA enrollment secret for user {UserId}")]
    private partial void LogEnrollmentSecretGenerated(string userId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Issued MFA challenge for user {UserId}")]
    private partial void LogChallengeIssued(string userId);

    [LoggerMessage(Level = LogLevel.Information, Message = "MFA challenge validated for user {UserId}")]
    private partial void LogChallengeValidated(string userId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "MFA challenge validation failed for user {UserId}, attempt {AttemptCount}")]
    private partial void LogChallengeValidationFailed(string userId, int attemptCount);

    [LoggerMessage(Level = LogLevel.Warning, Message = "MFA challenge locked for user {UserId} after {FailureCount} failed attempts")]
    private partial void LogChallengeLocked(string userId, int failureCount);

    [LoggerMessage(Level = LogLevel.Information, Message = "MFA backup code used for user {UserId}")]
    private partial void LogBackupCodeUsed(string userId);
}
