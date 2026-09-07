using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text.Json;
using Wallow.Notifications.Infrastructure.Services;

namespace Wallow.Notifications.Tests.Infrastructure.Services;

public sealed class WebPushCredentialsTests
{
    [Fact]
    public void RotationRetainsOldSigningKeyAndRetirementCannotBeReversed()
    {
        using ECDsa signingKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        ECParameters parameters = signingKey.ExportParameters(true);
        string publicKey = Base64Url.EncodeToString([4, .. parameters.Q.X!, .. parameters.Q.Y!]);
        string privateKey = Base64Url.EncodeToString(parameters.D!);
        WebPushSigningKey oldKey = new("old", publicKey, privateKey, false);
        WebPushCredentials initial = new("mailto:push@example.com", "old", [oldKey]);
        initial.IsValidReplacementFor(null).Should().BeTrue();
        WebPushCredentials retired = initial with { CurrentKeyId = null, Keys = [oldKey with { Retired = true, PrivateKey = null }] };
        retired.IsValidReplacementFor(initial).Should().BeTrue();
        initial.IsValidReplacementFor(retired).Should().BeFalse();
        JsonSerializer.Deserialize<WebPushCredentials>(JsonSerializer.Serialize(initial)).Should().NotBeNull();
    }
}
