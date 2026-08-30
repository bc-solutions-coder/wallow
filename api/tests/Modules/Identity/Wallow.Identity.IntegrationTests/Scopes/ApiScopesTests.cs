using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Wallow.Identity.Application.DTOs;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Entities;
using Wallow.Tests.Common.Factories;

namespace Wallow.Identity.IntegrationTests.Scopes;

[Trait("Category", "Integration")]
public class ApiScopesTests(WallowApiFactory factory) : IdentityIntegrationTestBase(factory)
{
    private IApiScopeRepository ApiScopeRepository => ScopedServices.GetRequiredService<IApiScopeRepository>();

    [Fact]
    public async Task Should_List_All_Available_Scopes()
    {
        IReadOnlyList<ApiScope> scopes = await ApiScopeRepository.GetAllAsync();

        scopes.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Should_List_Scopes_Via_API()
    {
        HttpResponseMessage response = await Client.GetAsync("/identity/scopes");

        response.IsSuccessStatusCode.Should().BeTrue();
        List<ApiScopeDto>? scopes = await response.Content.ReadFromJsonAsync<List<ApiScopeDto>>();
        scopes.Should().NotBeNull();
        scopes.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Should_Filter_Scopes_By_Category()
    {
        IReadOnlyList<ApiScope> allScopes = await ApiScopeRepository.GetAllAsync();
        string? firstCategory = allScopes.FirstOrDefault(s => s.Category != null)?.Category;

        if (firstCategory is null)
        {
            return;
        }

        IReadOnlyList<ApiScope> filteredScopes = await ApiScopeRepository.GetAllAsync(firstCategory);

        filteredScopes.Should().NotBeEmpty();
        filteredScopes.Should().OnlyContain(s => s.Category == firstCategory);
    }

    [Fact]
    public async Task Should_Filter_Scopes_By_Category_Via_API()
    {
        IReadOnlyList<ApiScope> allScopes = await ApiScopeRepository.GetAllAsync();
        string? firstCategory = allScopes.FirstOrDefault(s => s.Category != null)?.Category;

        if (firstCategory is null)
        {
            return;
        }

        HttpResponseMessage response = await Client.GetAsync($"/identity/scopes?category={firstCategory}");

        response.IsSuccessStatusCode.Should().BeTrue();
        List<ApiScopeDto>? scopes = await response.Content.ReadFromJsonAsync<List<ApiScopeDto>>();
        scopes.Should().NotBeNull();
        scopes.Should().NotBeEmpty();
        scopes.Should().OnlyContain(s => s.Category == firstCategory);
    }

    [Fact]
    public async Task Should_Include_Scope_Metadata()
    {
        IReadOnlyList<ApiScope> scopes = await ApiScopeRepository.GetAllAsync();

        scopes.Should().NotBeEmpty();
        ApiScope firstScope = scopes[0];
        firstScope.DisplayName.Should().NotBeNullOrWhiteSpace();
        firstScope.Category.Should().NotBeNullOrWhiteSpace();
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
