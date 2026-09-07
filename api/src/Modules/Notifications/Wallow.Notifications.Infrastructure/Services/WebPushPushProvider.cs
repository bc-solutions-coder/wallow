using System.Text.Json;
using Wallow.Notifications.Application.Channels.Push.Interfaces;
using Wallow.Notifications.Domain.Channels.Push;
using Wallow.Notifications.Domain.Channels.Push.Entities;

namespace Wallow.Notifications.Infrastructure.Services;

public sealed class WebPushPushProvider(HttpClient http, string credentialsJson, DeviceRegistration device) : IPushProvider
{
    public Task<PushDeliveryResult> SendAsync(PushMessage message, string deviceToken, CancellationToken cancellationToken = default)
    {
        WebPushCredentials? credentials = WebPushCredentials.Parse(credentialsJson);
        if (credentials is null || !credentials.IsValidReplacementFor(null))
        {
            return Task.FromResult(new PushDeliveryResult(false, "Web Push signing configuration is invalid"));
        }
        WebPushSigningKey? key = credentials.Keys.SingleOrDefault(key => key.Id == device.SigningKeyId && !key.Retired);
        if (key is null || device.Subscription is null
            || device.Subscription.Endpoint != deviceToken)
        {
            return Task.FromResult(new PushDeliveryResult(false, "Web Push signing key or subscription is unavailable"));
        }
        string payload = JsonSerializer.Serialize(new { title = message.Title, body = message.Body, clickPath = message.ClickPath });
        return new WebPushProtocol(http).SendAsync(device.Subscription, key, credentials.Subject, payload, cancellationToken);
    }
}

public sealed class UnavailableWebPushProvider : IPushProvider
{
    public Task<PushDeliveryResult> SendAsync(PushMessage message, string deviceToken, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new PushDeliveryResult(false, "Web Push is not configured or enabled"));
    }
}
