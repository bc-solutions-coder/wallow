using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Wallow.E2E.Tests.Infrastructure;

public static class TestUserFactory
{
    private const string TestPassword = "P@ssw0rd!Strong12";
    private const int MaxMailRetries = 15;
    private static readonly TimeSpan _mailPollInterval = TimeSpan.FromSeconds(2);

    public static async Task<TestUser> CreateAsync(string apiBaseUrl, string mailpitBaseUrl)
    {
        string email = $"e2e-{Guid.NewGuid():N}@test.local";

        await RegisterUserAsync(apiBaseUrl, email);
        await VerifyEmailAsync(mailpitBaseUrl, email);

        return new TestUser(email, TestPassword);
    }

    private static async Task RegisterUserAsync(string apiBaseUrl, string email)
    {
        using HttpClient httpClient = new() { Timeout = TimeSpan.FromSeconds(30) };

        object payload = new
        {
            email,
            password = TestPassword,
            confirmPassword = TestPassword,
            clientId = "wallow-web-client",
        };

        HttpResponseMessage response = await httpClient.PostAsJsonAsync(
            $"{apiBaseUrl}/api/v1/identity/auth/register", payload);

        response.EnsureSuccessStatusCode();
    }

    private static async Task VerifyEmailAsync(string mailpitBaseUrl, string email)
    {
        using HttpClient httpClient = new() { Timeout = TimeSpan.FromSeconds(30) };

        string verificationLink = string.Empty;

        for (int attempt = 0; attempt < MaxMailRetries; attempt++)
        {
            HttpResponseMessage response = await httpClient.GetAsync(
                $"{mailpitBaseUrl}/api/v1/search?query=to:{email}");

            if (response.IsSuccessStatusCode)
            {
                string json = await response.Content.ReadAsStringAsync();
                MailpitSearchResult? result = JsonSerializer.Deserialize<MailpitSearchResult>(json);

                if (result?.Messages is { Count: > 0 })
                {
                    // Check all messages — registration may send multiple emails
                    foreach (MailpitMessageSummary msg in result.Messages)
                    {
                        HttpResponseMessage msgResponse = await httpClient.GetAsync(
                            $"{mailpitBaseUrl}/api/v1/message/{msg.Id}");

                        if (msgResponse.IsSuccessStatusCode)
                        {
                            MailpitMessage? message = await msgResponse.Content.ReadFromJsonAsync<MailpitMessage>();
                            string body = message?.Text ?? message?.Html ?? string.Empty;

                            verificationLink = ExtractLinkContaining(body, "verify")
                                ?? ExtractLinkContaining(body, "confirm")
                                ?? string.Empty;

                            if (!string.IsNullOrEmpty(verificationLink))
                            {
                                break;
                            }
                        }
                    }

                    if (!string.IsNullOrEmpty(verificationLink))
                    {
                        break;
                    }
                }
            }

            await Task.Delay(_mailPollInterval);
        }

        if (string.IsNullOrEmpty(verificationLink))
        {
            throw new InvalidOperationException(
                $"Failed to retrieve verification email for {email} after {MaxMailRetries} attempts.");
        }

        // Visit the verification link to confirm the account
        HttpResponseMessage verifyResponse = await httpClient.GetAsync(verificationLink);
        verifyResponse.EnsureSuccessStatusCode();
    }

    private static string? ExtractLinkContaining(string body, string keyword)
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

    private sealed class MailpitSearchResult
    {
        [JsonPropertyName("messages")]
        public List<MailpitMessageSummary> Messages { get; set; } = [];
    }

    private sealed class MailpitMessageSummary
    {
        [JsonPropertyName("ID")]
        public string Id { get; set; } = string.Empty;
    }

    private sealed class MailpitMessage
    {
        [JsonPropertyName("Text")]
        public string? Text { get; set; }

        [JsonPropertyName("HTML")]
        public string? Html { get; set; }
    }
}
