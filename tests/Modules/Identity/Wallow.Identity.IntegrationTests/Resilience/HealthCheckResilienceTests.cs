namespace Wallow.Identity.IntegrationTests.Resilience;

/// <summary>
/// Tests health check endpoint resilience behavior.
/// Uses WireMock to simulate upstream service states.
/// </summary>
[Trait("Category", "Integration")]
public class HealthCheckResilienceTests : IClassFixture<IdentityResilienceTestFactory>, IAsyncLifetime
{
    private readonly IdentityResilienceTestFactory _factory;
    private HttpClient _appClient = null!;

    public HealthCheckResilienceTests(IdentityResilienceTestFactory factory)
    {
        _factory = factory;
    }

    public Task InitializeAsync()
    {
        _appClient = _factory.CreateClient();
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _appClient.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task HealthCheck_Endpoint_Returns_Response()
    {
        _factory.SetupAllEndpointsReturn200();

        HttpResponseMessage response = await _appClient.GetAsync("/health");

        response.Should().NotBeNull();
    }
}
