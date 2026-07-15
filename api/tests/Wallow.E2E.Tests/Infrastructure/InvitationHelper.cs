using System.Net.Http.Json;
using System.Text.Json;

namespace Wallow.E2E.Tests.Infrastructure;

internal static class InvitationHelper
{
    public static async Task<Guid> CreateInvitationAsync(
        string apiBaseUrl,
        string authCookie,
        string inviteeEmail)
    {
        using HttpClient httpClient = new() { Timeout = TimeSpan.FromSeconds(30) };
        httpClient.DefaultRequestHeaders.Add("Cookie", authCookie);

        HttpResponseMessage response = await httpClient.PostAsJsonAsync(
            $"{apiBaseUrl}/v1/identity/invitations",
            new { email = inviteeEmail });

        response.EnsureSuccessStatusCode();

        JsonElement result = await response.Content.ReadFromJsonAsync<JsonElement>();
        return result.GetProperty("id").GetGuid();
    }

    public static Task<string> SearchForInvitationLinkAsync(
        string mailpitBaseUrl,
        string recipientEmail,
        int maxRetries = 10,
        int pollIntervalMs = 1000)
    {
        return MailpitHelper.SearchForLinkAsync(
            mailpitBaseUrl,
            recipientEmail,
            "invitation",
            maxRetries,
            pollIntervalSeconds: pollIntervalMs / 1000);
    }
}
