using System.Net;
using System.Net.Http.Json;
using Wallow.Identity.Application.DTOs;
using Wallow.Identity.Domain.Enums;

namespace Wallow.Identity.IntegrationTests.ServiceAccounts;

[Trait("Category", "Integration")]
public class CreateServiceAccountTests(ServiceAccountTestFactory factory) : ServiceAccountIntegrationTestBase(factory)
{
    private static readonly string[] _invoicesAndPaymentsScopes = ["invoices.read", "payments.read"];
    private static readonly string[] _invoicesReadScope = ["invoices.read"];

    [Fact]
    public async Task Should_Create_ServiceAccount_With_Valid_ClientId_And_Secret()
    {
        CreateServiceAccountRequest request = new(
            Name: "Test Service Account",
            Description: "Test description",
            Scopes: _invoicesAndPaymentsScopes
        );

        ServiceAccountCreatedResult result = await ServiceAccountService.CreateAsync(request);

        result.Should().NotBeNull();
        result.ClientId.Should().NotBeNullOrWhiteSpace();
        result.ClientSecret.Should().NotBeNullOrWhiteSpace();
        result.TokenEndpoint.Should().Contain("token");
        result.Scopes.Should().BeEquivalentTo(request.Scopes);
    }

    [Fact]
    public async Task Should_Create_ServiceAccount_Via_API()
    {
        var apiRequest = new
        {
            name = "API Test Service Account",
            description = "Created via API",
            scopes = _invoicesReadScope
        };

        HttpResponseMessage response = await Client.PostAsJsonAsync("/api/identity/service-accounts", apiRequest);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        ServiceAccountCreatedResponse? content = await response.Content.ReadFromJsonAsync<ServiceAccountCreatedResponse>();
        content.Should().NotBeNull();
        content.ClientId.Should().NotBeNullOrWhiteSpace();
        content.ClientSecret.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Should_Store_Metadata_After_Creation()
    {
        CreateServiceAccountRequest request = new(
            Name: "Metadata Test Account",
            Description: "For metadata verification",
            Scopes: _invoicesReadScope
        );

        ServiceAccountCreatedResult result = await ServiceAccountService.CreateAsync(request);
        ServiceAccountDto? retrieved = await ServiceAccountService.GetAsync(result.Id);

        retrieved.Should().NotBeNull();
        retrieved.Name.Should().Be(request.Name);
        retrieved.Description.Should().Be(request.Description);
        retrieved.Scopes.Should().BeEquivalentTo(request.Scopes);
        retrieved.Status.Should().Be(ServiceAccountStatus.Active);
    }

    [Fact]
    public async Task Should_Fail_Create_With_Empty_Name()
    {
        CreateServiceAccountRequest request = new(
            Name: "",
            Description: "Test",
            Scopes: _invoicesReadScope
        );

        Func<Task> act = async () => await ServiceAccountService.CreateAsync(request);

        await act.Should().ThrowAsync<Exception>();
    }
}

public record ServiceAccountCreatedResponse
{
    public string Id { get; init; } = string.Empty;
    public string ClientId { get; init; } = string.Empty;
    public string ClientSecret { get; init; } = string.Empty;
    public string TokenEndpoint { get; init; } = string.Empty;
    public IReadOnlyList<string> Scopes { get; init; } = [];
}
