using System.Net.Http.Headers;
using Wallow.Web.Models;

namespace Wallow.Web.Services;

public sealed class AppRegistrationService(
    IHttpClientFactory httpClientFactory,
    TokenProvider tokenProvider) : IAppRegistrationService
{
    private const string BasePath = "api/v1/identity/apps";

    public async Task<List<AppModel>> GetAppsAsync(CancellationToken ct = default)
    {
        HttpClient client = CreateAuthenticatedClient();
        HttpResponseMessage response = await client.GetAsync(BasePath, ct);

        if (response.IsSuccessStatusCode)
        {
            List<AppModel>? apps = await response.Content.ReadFromJsonAsync<List<AppModel>>(ct);
            return apps ?? [];
        }

        return [];
    }

    public async Task<AppModel?> GetAppAsync(string clientId, CancellationToken ct = default)
    {
        HttpClient client = CreateAuthenticatedClient();
        HttpResponseMessage response = await client.GetAsync($"{BasePath}/{clientId}", ct);

        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<AppModel>(ct);
        }

        return null;
    }

    public async Task<RegisterAppResult> RegisterAppAsync(RegisterAppModel model, CancellationToken ct = default)
    {
        HttpClient client = CreateAuthenticatedClient();

        // Map Web model to API contract: DisplayName -> ClientName, Scopes -> RequestedScopes
        object apiRequest = new
        {
            ClientName = model.DisplayName,
            RequestedScopes = model.Scopes,
            ClientType = model.ClientType,
            RedirectUris = model.RedirectUris
        };

        HttpResponseMessage response = await client.PostAsJsonAsync($"{BasePath}/register", apiRequest, ct);

        if (response.IsSuccessStatusCode)
        {
            AppRegistrationApiResponse? apiResponse =
                await response.Content.ReadFromJsonAsync<AppRegistrationApiResponse>(ct);

            if (apiResponse is not null)
            {
                return new RegisterAppResult(
                    apiResponse.ClientId,
                    apiResponse.ClientSecret,
                    apiResponse.RegistrationAccessToken,
                    true,
                    null);
            }

            return new RegisterAppResult(null, null, null, false, "Failed to deserialize response");
        }

        string errorBody = await response.Content.ReadAsStringAsync(ct);
        return new RegisterAppResult(null, null, null, false, errorBody);
    }

    // MultipartFormDataContent.Dispose() disposes all added content parts
#pragma warning disable CA2000
    public async Task<bool> UpsertBrandingAsync(
        string clientId,
        string displayName,
        string? tagline,
        string? themeJson,
        Stream? logoStream,
        string? logoFileName,
        string? logoContentType,
        CancellationToken ct = default)
    {
        HttpClient client = CreateAuthenticatedClient();

        using MultipartFormDataContent content = new();
        content.Add(new StringContent(displayName), "DisplayName");

        if (!string.IsNullOrEmpty(tagline))
        {
            content.Add(new StringContent(tagline), "Tagline");
        }

        if (!string.IsNullOrEmpty(themeJson))
        {
            content.Add(new StringContent(themeJson), "ThemeJson");
        }

        if (logoStream is not null && !string.IsNullOrEmpty(logoFileName))
        {
            StreamContent logoContent = new(logoStream);
            logoContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(logoContentType ?? "application/octet-stream");
            content.Add(logoContent, "logo", logoFileName);
        }

        HttpResponseMessage response = await client.PostAsync($"{BasePath}/{clientId}/branding", content, ct);
        return response.IsSuccessStatusCode;
    }
#pragma warning restore CA2000

    private HttpClient CreateAuthenticatedClient()
    {
        HttpClient client = httpClientFactory.CreateClient("WallowApi");

        if (!string.IsNullOrEmpty(tokenProvider.AccessToken))
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenProvider.AccessToken);
        }

        return client;
    }

    // Matches the AppRegistrationResponse from the Identity API
    private sealed record AppRegistrationApiResponse(
        string ClientId,
        string ClientSecret,
        string RegistrationAccessToken);
}
