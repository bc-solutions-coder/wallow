using Wallow.Web.Models;

namespace Wallow.Web.Services;

public sealed class MfaApiClient(
    IHttpClientFactory httpClientFactory,
    TokenProvider tokenProvider) : IMfaApiClient
{
    private const string MfaBasePath = "api/v1/identity/mfa";

    private HttpClient CreateAuthenticatedClient()
    {
        HttpClient client = httpClientFactory.CreateClient("WallowApi");
        if (!string.IsNullOrEmpty(tokenProvider.AccessToken))
        {
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokenProvider.AccessToken);
        }
        return client;
    }

    public async Task<MfaStatusResponse?> GetMfaStatusAsync(CancellationToken ct = default)
    {
        HttpClient client = CreateAuthenticatedClient();
        HttpResponseMessage response = await client.GetAsync($"{MfaBasePath}/status", ct);

        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<MfaStatusResponse>(ct);
        }

        return null;
    }

    public async Task<bool> DisableMfaAsync(string password, CancellationToken ct = default)
    {
        HttpClient client = CreateAuthenticatedClient();
        HttpResponseMessage response = await client.PostAsJsonAsync(
            $"{MfaBasePath}/disable",
            new { password },
            ct);

        if (response.IsSuccessStatusCode)
        {
            DisableResult? result = await response.Content.ReadFromJsonAsync<DisableResult>(ct);
            return result?.Succeeded ?? false;
        }

        return false;
    }

    public async Task<List<string>?> RegenerateBackupCodesAsync(string password, CancellationToken ct = default)
    {
        HttpClient client = CreateAuthenticatedClient();
        HttpResponseMessage response = await client.PostAsJsonAsync(
            $"{MfaBasePath}/backup-codes/regenerate",
            new { password },
            ct);

        if (response.IsSuccessStatusCode)
        {
            RegenerateResult? result = await response.Content.ReadFromJsonAsync<RegenerateResult>(ct);
            return result?.Codes;
        }

        return null;
    }

    public async Task<string?> IssueEnrollmentTokenAsync(CancellationToken ct = default)
    {
        HttpClient client = CreateAuthenticatedClient();
        HttpResponseMessage response = await client.PostAsync($"{MfaBasePath}/enroll/issue-token", null, ct);

        if (response.IsSuccessStatusCode)
        {
            EnrollmentTokenResult? result = await response.Content.ReadFromJsonAsync<EnrollmentTokenResult>(ct);
            return result?.Token;
        }

        return null;
    }

    private sealed record DisableResult(bool Succeeded);
    private sealed record RegenerateResult(List<string>? Codes);
    private sealed record EnrollmentTokenResult(string? Token);
}
