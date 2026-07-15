using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Wallow.Notifications.Application.Channels.Push.Interfaces;
using Wallow.Notifications.Domain.Channels.Push.Entities;

namespace Wallow.Notifications.Infrastructure.Services;

public sealed partial class ApnsPushProvider(
    HttpClient http,
    ILogger<ApnsPushProvider> logger) : IPushProvider
{
    public async Task<PushDeliveryResult> SendAsync(PushMessage message, string deviceToken, CancellationToken cancellationToken = default)
    {
        string url = $"https://api.push.apple.com/3/device/{deviceToken}";

        using HttpRequestMessage request = new(HttpMethod.Post, url);
        request.Headers.TryAddWithoutValidation("apns-topic", "com.wallow.app");
        request.Headers.TryAddWithoutValidation("apns-push-type", "alert");

        JsonElement payload = JsonSerializer.SerializeToElement(new
        {
            aps = new
            {
                alert = new
                {
                    title = message.Title,
                    body = message.Body
                }
            }
        });

        request.Content = new StringContent(payload.GetRawText(), Encoding.UTF8, "application/json");

        try
        {
            using HttpResponseMessage response = await http.SendAsync(request, cancellationToken);
            string json = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                LogPushSent(logger, deviceToken);
                return new PushDeliveryResult(true, null);
            }

            string errorMessage = $"APNs returned {(int)response.StatusCode}: {json}";
            LogPushFailed(logger, deviceToken, errorMessage);
            return new PushDeliveryResult(false, errorMessage);
        }
        catch (Exception ex)
        {
            LogPushException(logger, ex, deviceToken);
            return new PushDeliveryResult(false, ex.Message);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "APNs push notification sent successfully to device {DeviceToken}")]
    private static partial void LogPushSent(ILogger logger, string deviceToken);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to send APNs push notification to device {DeviceToken}: {Error}")]
    private static partial void LogPushFailed(ILogger logger, string deviceToken, string error);

    [LoggerMessage(Level = LogLevel.Error, Message = "Exception sending APNs push notification to device {DeviceToken}")]
    private static partial void LogPushException(ILogger logger, Exception ex, string deviceToken);
}
