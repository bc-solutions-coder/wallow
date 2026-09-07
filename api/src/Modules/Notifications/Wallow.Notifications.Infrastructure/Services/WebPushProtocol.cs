using System.Globalization;
using System.Text;
using Lib.Net.Http.WebPush;
using Lib.Net.Http.WebPush.Authentication;
using Wallow.Notifications.Application.Channels.Push.Interfaces;
using Wallow.Notifications.Domain.Channels.Push;

namespace Wallow.Notifications.Infrastructure.Services;

public sealed class WebPushProtocol(HttpClient http)
{
    public async Task<PushDeliveryResult> SendAsync(
        WebPushSubscription subscription,
        WebPushSigningKey signingKey,
        string subject,
        string payload,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!WebPushTransport.IsSafeEndpoint(subscription.Endpoint))
        {
            return new(false, "Web Push subscription endpoint is invalid.");
        }

        if (Encoding.UTF8.GetByteCount(payload) > 3993)
        {
            return new(false, "Web Push payload exceeds the 3993-byte limit.");
        }

        try
        {
            using VapidAuthentication authentication = new(signingKey.PublicKey, signingKey.PrivateKey!) { Subject = subject };
            PushServiceClient client = new(http) { AutoRetryAfter = false, DefaultTimeToLive = 86400 };
            PushSubscription deliverySubscription = new() { Endpoint = subscription.Endpoint };
            deliverySubscription.SetKey(PushEncryptionKeyName.P256DH, subscription.Keys.P256dh);
            deliverySubscription.SetKey(PushEncryptionKeyName.Auth, subscription.Keys.Auth);
            await client.RequestPushMessageDeliveryAsync(deliverySubscription, new(payload), authentication, cancellationToken);
            return new(true, null);
        }
        catch (PushServiceClientException exception)
        {
            int status = (int)exception.StatusCode;
            bool retryable = status == 429 || status is >= 500 and <= 599;
            TimeSpan? retryAfter = exception.Headers?.RetryAfter?.Delta;
            if (retryAfter is null && exception.Headers?.RetryAfter?.Date is DateTimeOffset date)
            {
                retryAfter = date - DateTimeOffset.UtcNow;
            }

            if (retryAfter is TimeSpan delay)
            {
                retryAfter = TimeSpan.FromSeconds(Math.Clamp(delay.TotalSeconds, 1, 86400));
            }

            return new(false, "Web Push service returned HTTP " + status.ToString(CultureInfo.InvariantCulture) + ".",
                status is 404 or 410, retryable, retryable ? retryAfter : null);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new(false, "Web Push service timed out.", Retryable: true);
        }
        catch (HttpRequestException)
        {
            return new(false, "Web Push service connection failed.", Retryable: true);
        }
        catch (Exception exception) when (exception is ArgumentException or FormatException or System.Security.Cryptography.CryptographicException or InvalidOperationException)
        {
            return new(false, "Web Push subscription or signing credentials are invalid.");
        }
    }
}
