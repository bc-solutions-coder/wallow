using Microsoft.Extensions.Logging;
using Wallow.Notifications.Application.Channels.Push.Interfaces;
using Wallow.Notifications.Domain.Channels.Push.Enums;

namespace Wallow.Notifications.Infrastructure.Services;

public sealed class PushProviderFactory(
    ITenantPushConfigurationRepository configurationRepository,
    IPushCredentialEncryptor credentialEncryptor,
    IHttpClientFactory httpClientFactory,
    ILoggerFactory loggerFactory) : IPushProviderFactory
{
    public async Task<IPushProvider> GetProviderAsync(PushPlatform platform)
    {
        Domain.Channels.Push.Entities.TenantPushConfiguration? config = await configurationRepository
            .GetByPlatformAsync(platform);

        if (config is { IsEnabled: true })
        {
            string credentials = credentialEncryptor.Decrypt(config.EncryptedCredentials);
            HttpClient httpClient = httpClientFactory.CreateClient($"Push_{platform}");

            return platform switch
            {
                PushPlatform.Fcm => new FcmPushProvider(httpClient, credentials, loggerFactory.CreateLogger<FcmPushProvider>()),
                PushPlatform.Apns => new ApnsPushProvider(httpClient, loggerFactory.CreateLogger<ApnsPushProvider>()),
                PushPlatform.WebPush => new WebPushPushProvider(httpClient, loggerFactory.CreateLogger<WebPushPushProvider>()),
                _ => new LogPushProvider(loggerFactory.CreateLogger<LogPushProvider>())
            };
        }

        return new LogPushProvider(loggerFactory.CreateLogger<LogPushProvider>());
    }
}
