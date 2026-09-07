using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text.Json;
using Wallow.Notifications.Application.Channels.Push.Interfaces;
using Wallow.Notifications.Domain.Channels.Push;
using Wallow.Notifications.Domain.Channels.Push.Entities;
using Wallow.Notifications.Domain.Channels.Push.Enums;
using Wallow.Shared.Kernel.MultiTenancy;

namespace Wallow.Notifications.Infrastructure.Services;

public sealed class WebPushConfiguration(
    ITenantPushConfigurationRepository repository,
    IPushCredentialEncryptor encryptor,
    TimeProvider timeProvider,
    ITenantContext tenantContext) : IWebPushConfiguration
{
    public async Task<WebPushPublicKey?> RotateAsync(string subject, CancellationToken cancellationToken)
    {
        TenantPushConfiguration? configuration = await repository.GetByPlatformAsync(PushPlatform.WebPush, cancellationToken);
        WebPushCredentials? previous = configuration is null ? null : WebPushCredentials.Parse(encryptor.Decrypt(configuration.EncryptedCredentials));
        if (previous is not null && !previous.IsValidReplacementFor(null)) { return null; }
        using ECDsa signer = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        ECParameters parameters = signer.ExportParameters(true);
        WebPushSigningKey key = new(Guid.NewGuid().ToString("N"),
            Base64Url.EncodeToString([4, .. parameters.Q.X!, .. parameters.Q.Y!]), Base64Url.EncodeToString(parameters.D!), false);
        WebPushCredentials next = new(subject, key.Id, [.. previous?.Keys ?? [], key]);
        if (!next.IsValidReplacementFor(previous)) { return null; }
        string encrypted = encryptor.Encrypt(JsonSerializer.Serialize(next, JsonSerializerOptions.Web));
        if (configuration is null)
        {
            configuration = TenantPushConfiguration.Create(TenantScope.Require(tenantContext.TenantId), PushPlatform.WebPush, encrypted, timeProvider);
        }
        else { configuration.UpdateCredentials(encrypted, timeProvider); }
        await repository.UpsertAsync(configuration, cancellationToken);
        return new WebPushPublicKey(key.Id, key.PublicKey);
    }

    public async Task<bool> RetireAsync(string keyId, CancellationToken cancellationToken)
    {
        TenantPushConfiguration? configuration = await repository.GetByPlatformAsync(PushPlatform.WebPush, cancellationToken);
        WebPushCredentials? previous = configuration is null ? null : WebPushCredentials.Parse(encryptor.Decrypt(configuration.EncryptedCredentials));
        if (configuration is null || previous is null || !previous.IsValidReplacementFor(null) || !previous.Keys.Any(key => key.Id == keyId)) { return false; }
        WebPushCredentials next = previous with
        {
            CurrentKeyId = previous.CurrentKeyId == keyId ? null : previous.CurrentKeyId,
            Keys = previous.Keys.Select(key => key.Id == keyId ? key with { Retired = true, PrivateKey = null } : key).ToArray(),
        };
        configuration.UpdateCredentials(encryptor.Encrypt(JsonSerializer.Serialize(next, JsonSerializerOptions.Web)), timeProvider);
        await repository.UpsertAsync(configuration, cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<WebPushKeyVersion>> GetVersionsAsync(CancellationToken cancellationToken)
    {
        TenantPushConfiguration? configuration = await repository.GetByPlatformAsync(PushPlatform.WebPush, cancellationToken);
        WebPushCredentials? credentials = configuration is null ? null : WebPushCredentials.Parse(encryptor.Decrypt(configuration.EncryptedCredentials));
        if (credentials is not null && !credentials.IsValidReplacementFor(null)) { return []; }
        return credentials?.Keys.Select(key => new WebPushKeyVersion(key.Id, key.PublicKey, key.Retired, key.Id == credentials.CurrentKeyId)).ToArray() ?? [];
    }

    public async Task<bool> IsAvailableAsync(string? keyId, CancellationToken cancellationToken)
    {
        WebPushCredentials? credentials = await GetCredentialsAsync(cancellationToken);
        return credentials is not null && credentials.Keys.Any(key => key.Id == keyId && !key.Retired);
    }

    public async Task<WebPushPublicKey?> GetCurrentKeyAsync(CancellationToken cancellationToken)
    {
        WebPushCredentials? credentials = await GetCredentialsAsync(cancellationToken);
        WebPushSigningKey? key = credentials?.Keys.SingleOrDefault(key => key.Id == credentials.CurrentKeyId && !key.Retired);
        return key is null ? null : new WebPushPublicKey(key.Id, key.PublicKey);
    }

    public async Task<bool> CanRegisterAsync(WebPushSubscription subscription, string keyId, bool existingSubscription, CancellationToken cancellationToken)
    {
        if (!WebPushTransport.IsSafeEndpoint(subscription.Endpoint) || subscription.Endpoint.Length > 2048
            || subscription.Endpoint.Any(character => character > 127)
            || subscription.Keys is null
            || (subscription.ExpirationTime is long expiration && expiration <= timeProvider.GetUtcNow().ToUnixTimeMilliseconds()))
        {
            return false;
        }
        try
        {
            byte[] publicKey = Base64Url.DecodeFromChars(subscription.Keys.P256dh);
            byte[] auth = Base64Url.DecodeFromChars(subscription.Keys.Auth);
            if (publicKey.Length != 65 || publicKey[0] != 4 || auth.Length != 16) { return false; }
            using ECDiffieHellman key = ECDiffieHellman.Create(new ECParameters
            {
                Curve = ECCurve.NamedCurves.nistP256,
                Q = new ECPoint { X = publicKey[1..33], Y = publicKey[33..65] },
            });
        }
        catch (Exception exception) when (exception is ArgumentException or FormatException or CryptographicException)
        {
            return false;
        }
        WebPushCredentials? credentials = await GetCredentialsAsync(cancellationToken);
        return credentials is not null && (existingSubscription || credentials.CurrentKeyId == keyId)
            && credentials.Keys.Any(key => key.Id == keyId && !key.Retired);
    }

    public Task<bool> ValidateReplacementAsync(string credentials, string? previousCredentials, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        WebPushCredentials? next = WebPushCredentials.Parse(credentials);
        WebPushCredentials? previous = previousCredentials is null ? null : WebPushCredentials.Parse(encryptor.Decrypt(previousCredentials));
        return Task.FromResult(next is not null && next.IsValidReplacementFor(previous));
    }

    public async Task<WebPushCredentials?> GetCredentialsAsync(CancellationToken cancellationToken)
    {
        TenantPushConfiguration? configuration = await repository.GetByPlatformAsync(PushPlatform.WebPush, cancellationToken);
        if (configuration is not { IsEnabled: true }) { return null; }
        try
        {
            WebPushCredentials? credentials = WebPushCredentials.Parse(encryptor.Decrypt(configuration.EncryptedCredentials));
            return credentials is not null && credentials.IsValidReplacementFor(null) ? credentials : null;
        }
        catch (CryptographicException)
        {
            return null;
        }
    }
}
