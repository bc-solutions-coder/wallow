using System.Net.Http.Json;
using Foundry.Identity.Application.DTOs;

namespace Foundry.Identity.IntegrationTests.ServiceAccounts;

[Trait("Category", "Integration")]
public class RotateSecretTests : ServiceAccountIntegrationTestBase
{
    private static readonly string[] _invoicesReadScope = ["invoices.read"];

    public RotateSecretTests(ServiceAccountTestFactory factory) : base(factory) { }

    [Fact]
    public async Task Should_Rotate_Secret_Successfully()
    {
        CreateServiceAccountRequest createRequest = new(
            "Rotation Test Account",
            "For secret rotation",
            _invoicesReadScope
        );

        ServiceAccountCreatedResult created = await ServiceAccountService.CreateAsync(createRequest);
        string originalSecret = created.ClientSecret;

        SecretRotatedResult rotated = await ServiceAccountService.RotateSecretAsync(created.Id);

        rotated.Should().NotBeNull();
        rotated.NewClientSecret.Should().NotBeNullOrWhiteSpace();
        rotated.NewClientSecret.Should().NotBe(originalSecret);
        rotated.RotatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Should_Rotate_Secret_Via_API()
    {
        CreateServiceAccountRequest createRequest = new(
            "API Rotation Test",
            "For API rotation test",
            _invoicesReadScope
        );

        ServiceAccountCreatedResult created = await ServiceAccountService.CreateAsync(createRequest);

        HttpResponseMessage response = await Client.PostAsync($"/api/identity/service-accounts/{created.Id.Value}/rotate-secret", null);

        response.IsSuccessStatusCode.Should().BeTrue();
        SecretRotatedResponse? result = await response.Content.ReadFromJsonAsync<SecretRotatedResponse>();
        result.Should().NotBeNull();
        result.NewClientSecret.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Should_Fail_Rotate_For_NonExistent_Account()
    {
        Guid nonExistentId = Guid.NewGuid();

        HttpResponseMessage response = await Client.PostAsync($"/api/identity/service-accounts/{nonExistentId}/rotate-secret", null);

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Old_Secret_Should_Be_Invalidated_After_Rotation()
    {
        CreateServiceAccountRequest createRequest = new(
            "Invalidation Test",
            "Test old secret invalidation",
            _invoicesReadScope
        );

        ServiceAccountCreatedResult created = await ServiceAccountService.CreateAsync(createRequest);
        string originalSecret = created.ClientSecret;

        await ServiceAccountService.RotateSecretAsync(created.Id);

        originalSecret.Should().NotBeNullOrWhiteSpace();
    }
}

public record SecretRotatedResponse
{
    public string NewClientSecret { get; init; } = string.Empty;
    public DateTime RotatedAt { get; init; }
}
