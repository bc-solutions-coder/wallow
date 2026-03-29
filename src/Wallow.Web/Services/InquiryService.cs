using Wallow.Web.Models;

namespace Wallow.Web.Services;

public sealed class InquiryService(
    IHttpClientFactory httpClientFactory,
    TokenProvider tokenProvider) : IInquiryService
{
    private const string BasePath = "api/v1/inquiries";

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

    public async Task<bool> SubmitInquiryAsync(InquiryModel model, CancellationToken ct = default)
    {
        HttpClient client = CreateAuthenticatedClient();
        HttpResponseMessage response = await client.PostAsJsonAsync(BasePath, model, ct);

        return response.IsSuccessStatusCode;
    }
}
