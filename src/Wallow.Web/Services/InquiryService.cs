using System.Net.Http.Headers;
using Microsoft.AspNetCore.Authentication;
using Wallow.Web.Models;

namespace Wallow.Web.Services;

public sealed class InquiryService(
    IHttpClientFactory httpClientFactory,
    IHttpContextAccessor httpContextAccessor) : IInquiryService
{
    private const string BasePath = "api/v1/inquiries";

    public async Task<bool> SubmitInquiryAsync(InquiryModel model, CancellationToken ct = default)
    {
        HttpClient client = await CreateAuthenticatedClientAsync();
        HttpResponseMessage response = await client.PostAsJsonAsync(BasePath, model, ct);

        return response.IsSuccessStatusCode;
    }

    private async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        HttpClient client = httpClientFactory.CreateClient("WallowApi");
        string? token = await httpContextAccessor.HttpContext!.GetTokenAsync("access_token");

        if (!string.IsNullOrEmpty(token))
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return client;
    }
}
