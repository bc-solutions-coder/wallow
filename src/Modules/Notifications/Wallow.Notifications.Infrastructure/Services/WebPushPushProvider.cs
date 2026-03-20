using System.Text;
using System.Text.Json;
using Wallow.Notifications.Application.Channels.Push.Interfaces;
using Wallow.Notifications.Domain.Channels.Push.Entities;
using Microsoft.Extensions.Logging;

namespace Wallow.Notifications.Infrastructure.Services;

public sealed partial class WebPushPushProvider(
    HttpClient http,
    ILogger<WebPushPushProvider> logger) : IPushProvider
{
    public async Task<PushDeliveryResult> SendAsync(PushMessage message, string deviceToken, CancellationToken cancellationToken = default)
    {
        // deviceToken is the push subscription endpoint URL for WebPush
        string url = deviceToken;

        using HttpRequestMessage request = new(HttpMethod.Post, url);
        request.Headers.TryAddWithoutValidation("TTL", "86400");

        JsonElement payload = JsonSerializer.SerializeToElement(new
        {
            title = message.Title,
            body = message.Body
        });

        request.Content = new StringContent(payload.GetRawText(), Encoding.UTF8, "application/json");

        try
        {
            using HttpResponseMessage response = await http.SendAsync(request, cancellationToken);
            string responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                LogPushSent(logger, url);
                return new PushDeliveryResult(true, null);
            }

            string errorMessage = $"WebPush returned {(int)response.StatusCode}: {responseBody}";
            LogPushFailed(logger, url, errorMessage);
            return new PushDeliveryResult(false, errorMessage);
        }
        catch (Exception ex)
        {
            LogPushException(logger, ex, url);
            return new PushDeliveryResult(false, ex.Message);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "WebPush notification sent successfully to endpoint {Endpoint}")]
    private static partial void LogPushSent(ILogger logger, string endpoint);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to send WebPush notification to endpoint {Endpoint}: {Error}")]
    private static partial void LogPushFailed(ILogger logger, string endpoint, string error);

    [LoggerMessage(Level = LogLevel.Error, Message = "Exception sending WebPush notification to endpoint {Endpoint}")]
    private static partial void LogPushException(ILogger logger, Exception ex, string endpoint);
}
