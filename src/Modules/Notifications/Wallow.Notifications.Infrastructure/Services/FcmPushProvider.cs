using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Wallow.Notifications.Application.Channels.Push.Interfaces;
using Wallow.Notifications.Domain.Channels.Push.Entities;
using Microsoft.Extensions.Logging;

namespace Wallow.Notifications.Infrastructure.Services;

public sealed partial class FcmPushProvider(
    HttpClient http,
    string credential,
    ILogger<FcmPushProvider> logger) : IPushProvider
{
    public async Task<PushDeliveryResult> SendAsync(PushMessage message, string deviceToken, CancellationToken cancellationToken = default)
    {
        string url = "https://fcm.googleapis.com/v1/projects/-/messages:send";

        using HttpRequestMessage request = new(HttpMethod.Post, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", credential);

        JsonElement payload = JsonSerializer.SerializeToElement(new
        {
            message = new
            {
                token = deviceToken,
                notification = new
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

            string errorMessage = $"FCM returned {(int)response.StatusCode}: {json}";
            LogPushFailed(logger, deviceToken, errorMessage);
            return new PushDeliveryResult(false, errorMessage);
        }
        catch (Exception ex)
        {
            LogPushException(logger, ex, deviceToken);
            return new PushDeliveryResult(false, ex.Message);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Push notification sent successfully to device {DeviceToken}")]
    private static partial void LogPushSent(ILogger logger, string deviceToken);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to send push notification to device {DeviceToken}: {Error}")]
    private static partial void LogPushFailed(ILogger logger, string deviceToken, string error);

    [LoggerMessage(Level = LogLevel.Error, Message = "Exception sending push notification to device {DeviceToken}")]
    private static partial void LogPushException(ILogger logger, Exception ex, string deviceToken);
}
