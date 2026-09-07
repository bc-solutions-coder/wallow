using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text.Json;

namespace Wallow.Notifications.Infrastructure.Services;

public sealed record WebPushSigningKey(string Id, string PublicKey, string? PrivateKey, bool Retired);

public sealed record WebPushCredentials(string Subject, string? CurrentKeyId, IReadOnlyList<WebPushSigningKey> Keys)
{
    public static WebPushCredentials? Parse(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<WebPushCredentials>(json, JsonSerializerOptions.Web);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public bool IsValidReplacementFor(WebPushCredentials? previous)
    {
        if (!Uri.TryCreate(Subject, UriKind.Absolute, out Uri? subject)
            || (subject.Scheme != "mailto" && subject.Scheme != "https")
            || (subject.Scheme == "mailto" && (string.IsNullOrWhiteSpace(subject.UserInfo) || string.IsNullOrWhiteSpace(subject.Host)))
            || Keys is null || Keys.Count == 0
            || Keys.Any(key => key is null || string.IsNullOrWhiteSpace(key.Id) || key.Id.Length > 100)
            || Keys.Select(key => key.Id).Distinct(StringComparer.Ordinal).Count() != Keys.Count
            || Keys.Select(key => key.PublicKey).Distinct(StringComparer.Ordinal).Count() != Keys.Count
            || (CurrentKeyId is not null && !Keys.Any(key => key.Id == CurrentKeyId && !key.Retired)))
        {
            return false;
        }

        foreach (WebPushSigningKey key in Keys)
        {
            if (key.Retired && key.PrivateKey is not null) { return false; }
            try
            {
                byte[] publicKey = Base64Url.DecodeFromChars(key.PublicKey);
                if (publicKey.Length != 65 || publicKey[0] != 4 || Base64Url.EncodeToString(publicKey) != key.PublicKey) { return false; }
                using ECDsa signer = ECDsa.Create(new ECParameters
                {
                    Curve = ECCurve.NamedCurves.nistP256,
                    Q = new ECPoint { X = publicKey[1..33], Y = publicKey[33..65] },
                    D = key.Retired ? null : Base64Url.DecodeFromChars(key.PrivateKey!),
                });
                if (!key.Retired)
                {
                    byte[] challenge = [1];
                    byte[] proof = signer.SignData(challenge, HashAlgorithmName.SHA256);
                    using ECDsa verifier = ECDsa.Create(new ECParameters
                    {
                        Curve = ECCurve.NamedCurves.nistP256,
                        Q = new ECPoint { X = publicKey[1..33], Y = publicKey[33..65] },
                    });
                    if (!verifier.VerifyData(challenge, proof, HashAlgorithmName.SHA256)) { return false; }
                }
            }
            catch (Exception exception) when (exception is ArgumentException or FormatException or CryptographicException)
            {
                return false;
            }
        }

        return previous is null || previous.Keys.All(old => Keys.Any(key => key.Id == old.Id
            && key.PublicKey == old.PublicKey && (!old.Retired || key.Retired)));
    }
}
