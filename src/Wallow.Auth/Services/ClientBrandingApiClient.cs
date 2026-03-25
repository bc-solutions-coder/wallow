namespace Wallow.Auth.Services;

public sealed class ClientBrandingApiClient(IHttpClientFactory httpClientFactory) : IClientBrandingClient
{
    public async Task<ClientBrandingResponse?> GetBrandingAsync(string clientId, CancellationToken ct = default)
    {
        HttpClient client = httpClientFactory.CreateClient("AuthApi");
        HttpResponseMessage response = await client.GetAsync(
            $"api/v1/identity/apps/{Uri.EscapeDataString(clientId)}/branding", ct);

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadFromJsonAsync<ClientBrandingResponse>(ct);
    }
}
