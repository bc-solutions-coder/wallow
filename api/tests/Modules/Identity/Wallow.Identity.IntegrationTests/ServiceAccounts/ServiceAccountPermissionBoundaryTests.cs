using System.Net;
using System.Net.Http.Json;
using Wallow.Identity.Application.DTOs;

namespace Wallow.Identity.IntegrationTests.ServiceAccounts;

/// <summary>
/// Pins the per-action permission boundaries on the service-account endpoints (Wallow-y74w):
/// ServiceAccountsRead lists, ServiceAccountsWrite creates and updates scopes,
/// ServiceAccountsManage rotates secrets and revokes. The check is exact-match — no permission
/// implies another — and the relying-party client CRUD stays behind AdminAccess, which none of
/// the serviceaccounts.* scopes map to.
/// </summary>
[Trait("Category", "Integration")]
public class ServiceAccountPermissionBoundaryTests(ServiceAccountTestFactory factory)
    : ServiceAccountIntegrationTestBase(factory)
{
    private static readonly string[] _invoicesReadScope = ["invoices.read"];
    private static readonly string[] _updatedScopes = ["invoices.read", "payments.read"];

    private HttpClient CreateClientWithScopes(string scopes)
    {
        HttpClient client = Factory.CreateClient();
        client.DefaultRequestHeaders.Add("Authorization", "Bearer test-token");
        client.DefaultRequestHeaders.Add("X-Test-User-Id", Guid.NewGuid().ToString());
        client.DefaultRequestHeaders.Add("X-Test-Roles", "user");
        client.DefaultRequestHeaders.Add("X-Test-Scopes", scopes);
        return client;
    }

    private async Task<Guid> CreateAccountAsync(string name)
    {
        CreateServiceAccountRequest request = new(name, "Boundary test account", _invoicesReadScope);
        ServiceAccountCreatedResult created = await ServiceAccountService.CreateAsync(request);
        return created.Id.Value;
    }

    [Fact]
    public async Task ReadScope_Can_List_ServiceAccounts()
    {
        using HttpClient client = CreateClientWithScopes("serviceaccounts.read");

        HttpResponseMessage response = await client.GetAsync("/identity/clients/service-accounts");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ReadScope_Can_Get_ServiceAccount()
    {
        Guid id = await CreateAccountAsync("Read Get Target");
        using HttpClient client = CreateClientWithScopes("serviceaccounts.read");

        HttpResponseMessage response = await client.GetAsync($"/identity/clients/service-accounts/{id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ReadScope_Cannot_Create_ServiceAccount()
    {
        using HttpClient client = CreateClientWithScopes("serviceaccounts.read");

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/identity/clients/service-accounts",
            new { name = "Denied", description = "Denied", scopes = _invoicesReadScope });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ReadScope_Cannot_Update_Scopes()
    {
        Guid id = await CreateAccountAsync("Read UpdateScopes Target");
        using HttpClient client = CreateClientWithScopes("serviceaccounts.read");

        HttpResponseMessage response = await client.PutAsJsonAsync(
            $"/identity/clients/service-accounts/{id}/scopes",
            new { scopes = _invoicesReadScope });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ReadScope_Cannot_Rotate_Secret()
    {
        Guid id = await CreateAccountAsync("Read Rotate Target");
        using HttpClient client = CreateClientWithScopes("serviceaccounts.read");

        HttpResponseMessage response = await client.PostAsync(
            $"/identity/clients/service-accounts/{id}/rotate-secret",
            content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ReadScope_Cannot_Revoke_ServiceAccount()
    {
        Guid id = await CreateAccountAsync("Read Revoke Target");
        using HttpClient client = CreateClientWithScopes("serviceaccounts.read");

        HttpResponseMessage response = await client.DeleteAsync($"/identity/clients/service-accounts/{id}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task WriteScope_Can_Create_ServiceAccount()
    {
        using HttpClient client = CreateClientWithScopes("serviceaccounts.write");

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/identity/clients/service-accounts",
            new { name = "Write Created", description = "Created by write scope", scopes = _invoicesReadScope });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task WriteScope_Can_Update_Scopes()
    {
        Guid id = await CreateAccountAsync("Write UpdateScopes Target");
        using HttpClient client = CreateClientWithScopes("serviceaccounts.write");

        HttpResponseMessage response = await client.PutAsJsonAsync(
            $"/identity/clients/service-accounts/{id}/scopes",
            new { scopes = _updatedScopes });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task WriteScope_Cannot_Rotate_Secret()
    {
        Guid id = await CreateAccountAsync("Write Rotate Target");
        using HttpClient client = CreateClientWithScopes("serviceaccounts.write");

        HttpResponseMessage response = await client.PostAsync(
            $"/identity/clients/service-accounts/{id}/rotate-secret",
            content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task WriteScope_Cannot_Revoke_ServiceAccount()
    {
        Guid id = await CreateAccountAsync("Write Revoke Target");
        using HttpClient client = CreateClientWithScopes("serviceaccounts.write");

        HttpResponseMessage response = await client.DeleteAsync($"/identity/clients/service-accounts/{id}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ManageScope_Can_Rotate_Secret()
    {
        Guid id = await CreateAccountAsync("Manage Rotate Target");
        using HttpClient client = CreateClientWithScopes("serviceaccounts.manage");

        HttpResponseMessage response = await client.PostAsync(
            $"/identity/clients/service-accounts/{id}/rotate-secret",
            content: null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ManageScope_Can_Revoke_ServiceAccount()
    {
        Guid id = await CreateAccountAsync("Manage Revoke Target");
        using HttpClient client = CreateClientWithScopes("serviceaccounts.manage");

        HttpResponseMessage response = await client.DeleteAsync($"/identity/clients/service-accounts/{id}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task ManageScope_Alone_Cannot_List_ServiceAccounts()
    {
        // No hierarchy at the check: manage does not imply read. Full management is a grant-side
        // combination of all three scopes.
        using HttpClient client = CreateClientWithScopes("serviceaccounts.manage");

        HttpResponseMessage response = await client.GetAsync("/identity/clients/service-accounts");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ServiceAccountScopes_Do_Not_Grant_RelyingParty_Client_Surface()
    {
        // The /identity/clients CRUD is first-party platform administration (AdminAccess);
        // no serviceaccounts.* scope reaches it.
        using HttpClient client = CreateClientWithScopes(
            "serviceaccounts.read serviceaccounts.write serviceaccounts.manage");

        HttpResponseMessage response = await client.GetAsync("/identity/clients");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AdminRole_Retains_Full_ServiceAccount_Surface()
    {
        // The admin role grants all three permissions via RolePermissionMapping, so the
        // narrowing must not cost admins anything.
        Guid id = await CreateAccountAsync("Admin Rotate Target");

        HttpResponseMessage list = await Client.GetAsync("/identity/clients/service-accounts");
        HttpResponseMessage rotate = await Client.PostAsync(
            $"/identity/clients/service-accounts/{id}/rotate-secret",
            content: null);
        HttpResponseMessage revoke = await Client.DeleteAsync($"/identity/clients/service-accounts/{id}");

        list.StatusCode.Should().Be(HttpStatusCode.OK);
        rotate.StatusCode.Should().Be(HttpStatusCode.OK);
        revoke.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}
