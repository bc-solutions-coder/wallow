using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Entities;

namespace Wallow.Identity.Infrastructure.Services;

public sealed partial class MfaService : IMfaService
{
    private const string ProtectorPurpose = "Wallow.Identity.Mfa";
    private const int TotpDigits = 6;
    private const int TotpStepSeconds = 30;
    private const int BackupCodeCount = 10;
    private const int BackupCodeLength = 8;

    private readonly IDataProtector _protector;
    private readonly UserManager<WallowUser> _userManager;
    private readonly ILogger<MfaService> _logger;

    public MfaService(
        IDataProtectionProvider dataProtectionProvider,
        UserManager<WallowUser> userManager,
        ILogger<MfaService> logger)
    {
        _protector = dataProtectionProvider.CreateProtector(ProtectorPurpose);
        _userManager = userManager;
        _logger = logger;
    }

    public Task<(string Secret, string QrUri)> GenerateEnrollmentSecretAsync(string userId, CancellationToken ct)
    {
        byte[] secretBytes = RandomNumberGenerator.GetBytes(20);
        string base32Secret = ToBase32(secretBytes);
        string qrUri = $"otpauth://totp/Wallow:{userId}?secret={base32Secret}&issuer=Wallow&digits={TotpDigits}&period={TotpStepSeconds}";
        LogEnrollmentSecretGenerated(userId);
        return Task.FromResult((base32Secret, qrUri));
    }

    public Task<bool> ValidateTotpAsync(string base32Secret, string code, CancellationToken ct)
    {
        // The secret may be either raw base32 (during enrollment) or data-protected (during challenge).
        // Try to unprotect first; if that fails, treat as raw base32.
        string resolvedSecret;
        try
        {
            resolvedSecret = _protector.Unprotect(base32Secret);
        }
        catch (CryptographicException)
        {
            resolvedSecret = base32Secret;
        }

        byte[] secretBytes = FromBase32(resolvedSecret);
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

    public async Task<bool> ValidateBackupCodeAsync(string userId, string code, CancellationToken ct)
    {
        WallowUser? user = await _userManager.FindByIdAsync(userId);
        if (user is null || string.IsNullOrEmpty(user.BackupCodesHash))
        {
            return false;
        }

        string codeHash = ComputeBackupCodeHash(code);

        List<string>? storedHashes;
        try
        {
            storedHashes = JsonSerializer.Deserialize<List<string>>(user.BackupCodesHash);
        }
        catch (JsonException)
        {
            return false;
        }

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

    public string SerializeBackupCodesForStorage(IReadOnlyList<string> plainTextCodes)
    {
        List<string> hashed = plainTextCodes.Select(ComputeBackupCodeHash).ToList();
        return JsonSerializer.Serialize(hashed);
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

    [LoggerMessage(Level = LogLevel.Information, Message = "MFA backup code used for user {UserId}")]
    private partial void LogBackupCodeUsed(string userId);
}
