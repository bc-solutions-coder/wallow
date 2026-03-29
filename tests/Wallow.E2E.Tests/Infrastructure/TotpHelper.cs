using OtpNet;

namespace Wallow.E2E.Tests.Infrastructure;

public static class TotpHelper
{
    private const int TotpWindowSeconds = 30;

    public static string GenerateCode(string base32Secret)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(base32Secret);

        byte[] secretBytes = Base32Encoding.ToBytes(base32Secret);
        Totp totp = new(secretBytes);
        return totp.ComputeTotp();
    }

    /// <summary>
    /// Generates a TOTP code that is guaranteed to have at least <paramref name="minRemainingSeconds"/>
    /// of validity remaining. If the current window is about to expire, waits for the next window.
    /// </summary>
    public static async Task<string> GenerateFreshCodeAsync(string base32Secret, int minRemainingSeconds = 5)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(base32Secret);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(minRemainingSeconds);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(minRemainingSeconds, TotpWindowSeconds);

        long elapsedInWindow = DateTimeOffset.UtcNow.ToUnixTimeSeconds() % TotpWindowSeconds;
        int remainingSeconds = TotpWindowSeconds - (int)elapsedInWindow;

        if (remainingSeconds < minRemainingSeconds)
        {
            // Wait for the next window to start, plus a small buffer
            await Task.Delay(TimeSpan.FromSeconds(remainingSeconds + 1));
        }

        byte[] secretBytes = Base32Encoding.ToBytes(base32Secret);
        Totp totp = new(secretBytes);
        return totp.ComputeTotp();
    }
}
