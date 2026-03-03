using System.Net.Http.Json;
using Foundry.Identity.Application.DTOs;
using Foundry.Identity.Domain.Entities;

namespace Foundry.Identity.IntegrationTests.ServiceAccounts;

[Trait("Category", "Integration")]
public class ApiScopesTests : ServiceAccountIntegrationTestBase
{
    public ApiScopesTests(ServiceAccountTestFactory factory) : base(factory) { }

    [Fact]
    public async Task Should_List_All_Available_Scopes()
    {
        IReadOnlyList<ApiScope> scopes = await ApiScopeRepository.GetAllAsync();

        scopes.Should().NotBeEmpty();
        scopes.Should().Contain(s => s.Code.Contains("invoices") || s.Code.Contains("payments"));
    }

    [Fact]
    public async Task Should_List_Scopes_Via_API()
    {
        HttpResponseMessage response = await Client.GetAsync("/api/identity/scopes");

        response.IsSuccessStatusCode.Should().BeTrue();
        List<ApiScopeDto>? scopes = await response.Content.ReadFromJsonAsync<List<ApiScopeDto>>();
        scopes.Should().NotBeNull();
        scopes.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Should_Filter_Scopes_By_Category()
    {
        IReadOnlyList<ApiScope> billingScopes = await ApiScopeRepository.GetAllAsync("Billing");

        billingScopes.Should().NotBeEmpty();
        billingScopes.Should().OnlyContain(s => s.Category == "Billing");
    }

    [Fact]
    public async Task Should_Filter_Scopes_By_Category_Via_API()
    {
        HttpResponseMessage response = await Client.GetAsync("/api/identity/scopes?category=Billing");

        response.IsSuccessStatusCode.Should().BeTrue();
        List<ApiScopeDto>? scopes = await response.Content.ReadFromJsonAsync<List<ApiScopeDto>>();
        scopes.Should().NotBeNull();
        scopes.Should().NotBeEmpty();
        scopes.Should().OnlyContain(s => s.Category == "Billing");
    }

    [Fact]
    public async Task Should_Include_Scope_Metadata()
    {
        IReadOnlyList<ApiScope> scopes = await ApiScopeRepository.GetAllAsync();

        ApiScope? invoiceReadScope = scopes.FirstOrDefault(s => s.Code == "invoices.read");
        invoiceReadScope.Should().NotBeNull();
        invoiceReadScope.DisplayName.Should().NotBeNullOrWhiteSpace();
        invoiceReadScope.Category.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Should_Identify_Default_Scopes()
    {
        IReadOnlyList<ApiScope> scopes = await ApiScopeRepository.GetAllAsync();

        scopes.Should().Contain(s => s.IsDefault);
    }

    [Fact]
    public async Task Should_Return_Consistent_Scope_List()
    {
        IReadOnlyList<ApiScope> firstCall = await ApiScopeRepository.GetAllAsync();
        IReadOnlyList<ApiScope> secondCall = await ApiScopeRepository.GetAllAsync();

        firstCall.Select(s => s.Code).Should().BeEquivalentTo(secondCall.Select(s => s.Code));
    }
}
