using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Wallow.E2E.Tests.Infrastructure;

internal static class MailpitHelper
{
    public static async Task<string> SearchForLinkAsync(
        string mailpitBaseUrl,
        string recipientEmail,
        string linkKeyword,
        int maxRetries = 10,
        int pollIntervalSeconds = 2)
    {
        using HttpClient httpClient = new() { Timeout = TimeSpan.FromSeconds(30) };

        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            HttpResponseMessage response = await httpClient.GetAsync(
                $"{mailpitBaseUrl}/api/v1/search?query=to:{recipientEmail}");

            if (response.IsSuccessStatusCode)
            {
                string json = await response.Content.ReadAsStringAsync();
                MailpitSearchResult? result = System.Text.Json.JsonSerializer.Deserialize<MailpitSearchResult>(json);

                if (result?.Messages is { Count: > 0 })
                {
                    foreach (MailpitMessageSummary msg in result.Messages)
                    {
                        HttpResponseMessage msgResponse = await httpClient.GetAsync(
                            $"{mailpitBaseUrl}/api/v1/message/{msg.Id}");

                        if (msgResponse.IsSuccessStatusCode)
                        {
                            MailpitMessage? message = await System.Net.Http.Json.HttpContentJsonExtensions
                                .ReadFromJsonAsync<MailpitMessage>(msgResponse.Content);
                            string body = message?.Text ?? message?.Html ?? string.Empty;

                            string? link = ExtractLinkContaining(body, linkKeyword);
                            if (link is not null)
                            {
                                return link;
                            }
                        }
                    }
                }
            }

            await Task.Delay(TimeSpan.FromSeconds(pollIntervalSeconds));
        }

        return string.Empty;
    }

    public static async Task<string> SearchForCodeAsync(
        string mailpitBaseUrl,
        string recipientEmail,
        int maxRetries = 10,
        int pollIntervalSeconds = 2)
    {
        using HttpClient httpClient = new() { Timeout = TimeSpan.FromSeconds(30) };

        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            HttpResponseMessage response = await httpClient.GetAsync(
                $"{mailpitBaseUrl}/api/v1/search?query=to:{recipientEmail}");

            if (response.IsSuccessStatusCode)
            {
                string json = await response.Content.ReadAsStringAsync();
                MailpitSearchResult? result = System.Text.Json.JsonSerializer.Deserialize<MailpitSearchResult>(json);

                if (result?.Messages is { Count: > 0 })
                {
                    foreach (MailpitMessageSummary msg in result.Messages)
                    {
                        HttpResponseMessage msgResponse = await httpClient.GetAsync(
                            $"{mailpitBaseUrl}/api/v1/message/{msg.Id}");

                        if (msgResponse.IsSuccessStatusCode)
                        {
                            MailpitMessage? message = await System.Net.Http.Json.HttpContentJsonExtensions
                                .ReadFromJsonAsync<MailpitMessage>(msgResponse.Content);
                            string body = message?.Text ?? message?.Html ?? string.Empty;

                            Match match = Regex.Match(body, @"\b\d{6}\b");
                            if (match.Success)
                            {
                                return match.Value;
                            }
                        }
                    }
                }
            }

            await Task.Delay(TimeSpan.FromSeconds(pollIntervalSeconds));
        }

        return string.Empty;
    }

    internal static string? ExtractLinkContaining(string body, string keyword)
    {
        int searchIndex = 0;
        while (searchIndex < body.Length)
        {
            int httpIndex = body.IndexOf("http", searchIndex, StringComparison.OrdinalIgnoreCase);
            if (httpIndex < 0)
            {
                break;
            }

            int endIndex = body.IndexOfAny([' ', '"', '\'', '<', '\n', '\r'], httpIndex);
            if (endIndex < 0)
            {
                endIndex = body.Length;
            }

            string url = body[httpIndex..endIndex];
            if (url.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                return url;
            }

            searchIndex = endIndex;
        }

        return null;
    }
}

internal sealed class MailpitSearchResult
{
    [JsonPropertyName("messages")]
    public List<MailpitMessageSummary> Messages { get; set; } = [];
}

internal sealed class MailpitMessageSummary
{
    [JsonPropertyName("ID")]
    public string Id { get; set; } = string.Empty;
}

internal sealed class MailpitMessage
{
    [JsonPropertyName("Text")]
    public string? Text { get; set; }

    [JsonPropertyName("HTML")]
    public string? Html { get; set; }
}
