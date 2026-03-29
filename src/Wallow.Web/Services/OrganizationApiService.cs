using Wallow.Web.Models;

namespace Wallow.Web.Services;

public sealed class OrganizationApiService(
    IHttpClientFactory httpClientFactory,
    TokenProvider tokenProvider) : IOrganizationApiService
{
    private const string OrganizationsPath = "api/v1/identity/organizations";
    private const string ClientsPath = "api/v1/identity/clients";

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

    public async Task<List<OrganizationModel>> GetOrganizationsAsync(CancellationToken ct = default)
    {
        HttpClient client = CreateAuthenticatedClient();
        HttpResponseMessage response = await client.GetAsync(OrganizationsPath, ct);

        if (response.IsSuccessStatusCode)
        {
            List<OrganizationModel>? orgs = await response.Content.ReadFromJsonAsync<List<OrganizationModel>>(ct);
            return orgs ?? [];
        }

        return [];
    }

    public async Task<OrganizationModel?> GetOrganizationAsync(Guid orgId, CancellationToken ct = default)
    {
        HttpClient client = CreateAuthenticatedClient();
        HttpResponseMessage response = await client.GetAsync($"{OrganizationsPath}/{orgId}", ct);

        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<OrganizationModel>(ct);
        }

        return null;
    }

    public async Task<List<OrganizationMemberModel>> GetMembersAsync(Guid orgId, CancellationToken ct = default)
    {
        HttpClient client = CreateAuthenticatedClient();
        HttpResponseMessage response = await client.GetAsync($"{OrganizationsPath}/{orgId}/members", ct);

        if (response.IsSuccessStatusCode)
        {
            List<OrganizationMemberModel>? members = await response.Content.ReadFromJsonAsync<List<OrganizationMemberModel>>(ct);
            return members ?? [];
        }

        return [];
    }

    public async Task<List<ClientModel>> GetClientsByTenantAsync(Guid tenantId, CancellationToken ct = default)
    {
        HttpClient client = CreateAuthenticatedClient();
        HttpResponseMessage response = await client.GetAsync($"{ClientsPath}/by-tenant/{tenantId}", ct);

        if (response.IsSuccessStatusCode)
        {
            List<ClientModel>? clients = await response.Content.ReadFromJsonAsync<List<ClientModel>>(ct);
            return clients ?? [];
        }

        return [];
    }
}
