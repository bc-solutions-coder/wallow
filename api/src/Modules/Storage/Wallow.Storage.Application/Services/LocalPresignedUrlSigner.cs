using System.Security.Cryptography;
using System.Text;

namespace Wallow.Storage.Application.Services;

/// <summary>
/// Signs and validates the time-limited URLs the local storage provider hands out,
/// standing in for the request signing a real object store performs. The HMAC covers the
/// HTTP method, the storage key, and the expiry, so a download URL cannot authorize a write
/// and neither the key nor the deadline can be altered after minting. The signing key is
/// random per instance; registered as a singleton, that means a restart invalidates every
/// outstanding URL — acceptable for the development-only local provider, whose URLs live
/// for minutes.
/// </summary>
public sealed class LocalPresignedUrlSigner
{
    /// <summary>The method a presigned download URL is signed for.</summary>
    public const string DownloadMethod = "GET";

    /// <summary>The method a presigned upload URL is signed for.</summary>
    public const string UploadMethod = "PUT";

    private const int KeySizeBytes = 32;

    private readonly byte[] _key = RandomNumberGenerator.GetBytes(KeySizeBytes);

    /// <summary>
    /// Compute the signature for a method + storage key + expiry triple.
    /// </summary>
    /// <param name="method">The HTTP method the URL authorizes.</param>
    /// <param name="storageKey">The storage key the URL addresses.</param>
    /// <param name="expiresUnixSeconds">Unix timestamp (seconds) after which the URL is dead.</param>
    /// <returns>A lowercase-hex, URL-safe signature.</returns>
    public string Sign(string method, string storageKey, long expiresUnixSeconds)
    {
        byte[] payload = Encoding.UTF8.GetBytes($"{method}\n{storageKey}\n{expiresUnixSeconds}");
        byte[] hash = HMACSHA256.HashData(_key, payload);
        return Convert.ToHexStringLower(hash);
    }

    /// <summary>
    /// Check a presented signature against the method, storage key, and expiry it claims to
    /// cover, and reject anything past its deadline.
    /// </summary>
    /// <param name="method">The HTTP method the request is attempting.</param>
    /// <param name="storageKey">The storage key the request addresses.</param>
    /// <param name="expiresUnixSeconds">The expiry the URL claims, as signed at minting.</param>
    /// <param name="signature">The signature presented with the request.</param>
    /// <returns>True only when the signature is authentic and the deadline has not passed.</returns>
    public bool Validate(string method, string storageKey, long expiresUnixSeconds, string signature)
    {
        if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() > expiresUnixSeconds)
        {
            return false;
        }

        byte[] expected = Encoding.UTF8.GetBytes(Sign(method, storageKey, expiresUnixSeconds));
        byte[] presented = Encoding.UTF8.GetBytes(signature);
        return CryptographicOperations.FixedTimeEquals(expected, presented);
    }
}
