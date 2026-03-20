using System.Net;
using Wallow.Identity.Application.DTOs;

namespace Wallow.Identity.IntegrationTests.ServiceAccounts;

[Trait("Category", "Integration")]
public class RevokeServiceAccountTests(ServiceAccountTestFactory factory) : ServiceAccountIntegrationTestBase(factory)
{
    private static readonly string[] _invoicesReadScope = ["invoices.read"];

    [Fact(Skip = "Flaky when run with full test suite")]
    public async Task Should_Revoke_ServiceAccount_Successfully()
    {
        CreateServiceAccountRequest createRequest = new(
            "Revoke Test Account",
            "For revocation test",
            _invoicesReadScope
        );

        ServiceAccountCreatedResult created = await ServiceAccountService.CreateAsync(createRequest);

        await ServiceAccountService.RevokeAsync(created.Id);

        ServiceAccountDto? retrieved = await ServiceAccountService.GetAsync(created.Id);
        retrieved.Should().BeNull();
    }

    [Fact(Skip = "Flaky when run with full test suite")]
    public async Task Should_Revoke_ServiceAccount_Via_API()
    {
        CreateServiceAccountRequest createRequest = new(
            "API Revoke Test",
            "For API revoke test",
            _invoicesReadScope
        );

        ServiceAccountCreatedResult created = await ServiceAccountService.CreateAsync(createRequest);

        HttpResponseMessage response = await Client.DeleteAsync($"/api/identity/service-accounts/{created.Id.Value}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        HttpResponseMessage getResponse = await Client.GetAsync($"/api/identity/service-accounts/{created.Id.Value}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact(Skip = "Flaky when run with full test suite")]
    public async Task Should_Fail_Revoke_NonExistent_Account()
    {
        Guid nonExistentId = Guid.NewGuid();

        HttpResponseMessage response = await Client.DeleteAsync($"/api/identity/service-accounts/{nonExistentId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact(Skip = "Flaky when run with full test suite")]
    public async Task Should_Not_List_Revoked_Account()
    {
        CreateServiceAccountRequest createRequest = new(
            "List After Revoke Test",
            "Should not appear in list after revoke",
            _invoicesReadScope
        );

        ServiceAccountCreatedResult created = await ServiceAccountService.CreateAsync(createRequest);
        await ServiceAccountService.RevokeAsync(created.Id);

        IReadOnlyList<ServiceAccountDto> accounts = await ServiceAccountService.ListAsync();

        accounts.Should().NotContain(a => a.Id == created.Id);
    }

    [Fact(Skip = "Flaky when run with full test suite")]
    public async Task Should_Prevent_Operations_On_Revoked_Account()
    {
        CreateServiceAccountRequest createRequest = new(
            "Prevent Operations Test",
            "Test operations on revoked account",
            _invoicesReadScope
        );

        ServiceAccountCreatedResult created = await ServiceAccountService.CreateAsync(createRequest);
        await ServiceAccountService.RevokeAsync(created.Id);

        HttpResponseMessage rotateResponse = await Client.PostAsync($"/api/identity/service-accounts/{created.Id.Value}/rotate-secret", null);

        rotateResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
