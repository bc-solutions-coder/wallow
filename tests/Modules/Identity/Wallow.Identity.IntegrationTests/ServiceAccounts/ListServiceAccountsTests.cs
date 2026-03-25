using System.Net.Http.Json;
using Wallow.Identity.Application.DTOs;

namespace Wallow.Identity.IntegrationTests.ServiceAccounts;

[Trait("Category", "Integration")]
public class ListServiceAccountsTests(ServiceAccountTestFactory factory) : ServiceAccountIntegrationTestBase(factory)
{
    private static readonly string[] _invoicesReadScope = ["invoices.read"];
    private static readonly string[] _paymentsReadScope = ["payments.read"];

    [Fact]
    public async Task Should_List_All_ServiceAccounts_For_Tenant()
    {
        CreateServiceAccountRequest request1 = new("Account 1", "First account", _invoicesReadScope);
        CreateServiceAccountRequest request2 = new("Account 2", "Second account", _paymentsReadScope);

        await ServiceAccountService.CreateAsync(request1);
        await ServiceAccountService.CreateAsync(request2);

        IReadOnlyList<ServiceAccountDto> accounts = await ServiceAccountService.ListAsync();

        accounts.Should().HaveCountGreaterThanOrEqualTo(2);
        accounts.Should().Contain(a => a.Name == "Account 1");
        accounts.Should().Contain(a => a.Name == "Account 2");
    }

    [Fact]
    public async Task Should_List_ServiceAccounts_Via_API()
    {
        CreateServiceAccountRequest request = new("API List Test", "Test account", _invoicesReadScope);
        await ServiceAccountService.CreateAsync(request);

        HttpResponseMessage response = await Client.GetAsync("/api/identity/service-accounts");

        response.IsSuccessStatusCode.Should().BeTrue();
        List<ServiceAccountDto>? accounts = await response.Content.ReadFromJsonAsync<List<ServiceAccountDto>>();
        accounts.Should().NotBeNull();
        accounts.Should().Contain(a => a.Name == "API List Test");
    }

    [Fact]
    public async Task Should_Only_Show_Accounts_For_Current_Tenant()
    {
        CreateServiceAccountRequest request = new("Tenant A Account", "Account for tenant A", _invoicesReadScope);
        ServiceAccountCreatedResult createdInTenantA = await ServiceAccountService.CreateAsync(request);

        Guid differentTenantId = Guid.NewGuid();
        HttpClient clientForTenantB = Factory.CreateClient();
        clientForTenantB.DefaultRequestHeaders.Add("Authorization", "Bearer test-token");
        clientForTenantB.DefaultRequestHeaders.Add("X-Tenant-Id", differentTenantId.ToString());

        HttpResponseMessage response = await clientForTenantB.GetAsync("/api/identity/service-accounts");
        List<ServiceAccountDto>? accounts = await response.Content.ReadFromJsonAsync<List<ServiceAccountDto>>();

        accounts.Should().NotBeNull();
        accounts.Should().NotContain(a => a.Id == createdInTenantA.Id);
    }

    [Fact]
    public async Task Should_Return_Empty_List_When_No_Accounts_Exist()
    {
        HttpClient client = Factory.CreateClient();
        client.DefaultRequestHeaders.Add("Authorization", "Bearer test-token");
        client.DefaultRequestHeaders.Add("X-Test-User-Id", Guid.NewGuid().ToString());

        HttpResponseMessage response = await client.GetAsync("/api/identity/service-accounts");
        List<ServiceAccountDto>? accounts = await response.Content.ReadFromJsonAsync<List<ServiceAccountDto>>();

        accounts.Should().NotBeNull();
        accounts.Should().BeEmpty();
    }
}
