using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Wallow.Notifications.Application.Channels.Sms.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Wallow.Notifications.Infrastructure.Services;

public sealed partial class TwilioSmsProvider(
    HttpClient http,
    IOptions<TwilioSettings> settings,
    ILogger<TwilioSmsProvider> logger) : ISmsProvider
{
    private readonly TwilioSettings _settings = settings.Value;

    public async Task<SmsDeliveryResult> SendAsync(string to, string body, CancellationToken cancellationToken = default)
    {
        string url = $"https://api.twilio.com/2010-04-01/Accounts/{_settings.AccountSid}/Messages.json";

        using HttpRequestMessage request = new(HttpMethod.Post, url);

        string credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{_settings.AccountSid}:{_settings.AuthToken}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);

        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["To"] = to,
            ["From"] = _settings.FromNumber,
            ["Body"] = body
        });

        try
        {
            using HttpResponseMessage response = await http.SendAsync(request, cancellationToken);
            string json = await response.Content.ReadAsStringAsync(cancellationToken);

            using JsonDocument doc = JsonDocument.Parse(json);
            JsonElement root = doc.RootElement;

            if (response.IsSuccessStatusCode)
            {
                string? sid = root.TryGetProperty("sid", out JsonElement sidElement) ? sidElement.GetString() : null;
                LogSmsSent(logger, to, sid);
                return new SmsDeliveryResult(true, sid, null);
            }

            string? errorMessage = root.TryGetProperty("message", out JsonElement msgElement) ? msgElement.GetString() : response.ReasonPhrase;
            LogSmsFailed(logger, to, errorMessage);
            return new SmsDeliveryResult(false, null, errorMessage);
        }
        catch (Exception ex)
        {
            LogSmsException(logger, ex, to);
            return new SmsDeliveryResult(false, null, ex.Message);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "SMS sent successfully to {To}, SID: {Sid}")]
    private static partial void LogSmsSent(ILogger logger, string to, string? sid);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to send SMS to {To}: {Error}")]
    private static partial void LogSmsFailed(ILogger logger, string to, string? error);

    [LoggerMessage(Level = LogLevel.Error, Message = "Exception sending SMS to {To}")]
    private static partial void LogSmsException(ILogger logger, Exception ex, string to);
}
