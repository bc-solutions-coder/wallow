using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Polly.CircuitBreaker;

namespace Foundry.Identity.IntegrationTests.Resilience;

[Trait("Category", "Integration")]
public class KeycloakCircuitBreakerTests : IClassFixture<KeycloakResilienceTestFactory>, IAsyncLifetime
{
    private readonly KeycloakResilienceTestFactory _factory;
    private IHttpClientFactory _httpClientFactory = null!;
    private IServiceScope? _scope;

    public KeycloakCircuitBreakerTests(KeycloakResilienceTestFactory factory)
    {
        _factory = factory;
    }

    public Task InitializeAsync()
    {
        // Ensure the host is created so DI is available
        _ = _factory.CreateClient();

        _scope = _factory.Services.CreateScope();
        _httpClientFactory = _scope.ServiceProvider.GetRequiredService<IHttpClientFactory>();

        _factory.SetupAllEndpointsReturn500();
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _scope?.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task CircuitBreaker_OpensAfterReachingFailureThreshold()
    {
        // The identity-provider profile has: FailureRatio=0.5, MinimumThroughput=10, SamplingDuration=30s
        // With 3 retries per request, each logical request generates up to 4 attempts.
        // After enough failed attempts exceeding MinimumThroughput with >50% failure ratio,
        // the circuit breaker should open and reject subsequent requests immediately.

        for (int i = 0; i < 5; i++)
        {
            try
            {
                HttpClient client = _httpClientFactory.CreateClient("KeycloakAdminClient");
                await client.GetAsync("/admin/realms/foundry/users");
            }
            catch
            {
                // Expected failures while tripping the circuit
            }
        }

        // By now the circuit should be open. The next request should fail with
        // BrokenCircuitException (Polly rejects immediately when circuit is open).
        Func<Task> act = async () =>
        {
            HttpClient client = _httpClientFactory.CreateClient("KeycloakAdminClient");
            await client.GetAsync("/admin/realms/foundry/users");
        };

        await act.Should().ThrowAsync<BrokenCircuitException>();
    }

    [Fact]
    public async Task CircuitBreaker_RecoversAfterBreakDuration()
    {
        FakeTimeProvider timeProvider = _factory.TimeProvider;

        // Trip the circuit breaker
        for (int i = 0; i < 5; i++)
        {
            try
            {
                HttpClient client = _httpClientFactory.CreateClient("KeycloakAdminClient");
                await client.GetAsync("/admin/realms/foundry/users");
            }
            catch
            {
                // Expected failures
            }
        }

        // Verify circuit is open
        Func<Task> actWhileOpen = async () =>
        {
            HttpClient client = _httpClientFactory.CreateClient("KeycloakAdminClient");
            await client.GetAsync("/admin/realms/foundry/users");
        };
        await actWhileOpen.Should().ThrowAsync<BrokenCircuitException>();

        // Switch WireMock to return 200 (healthy endpoint)
        _factory.SetupAllEndpointsReturn200();

        // Advance time past the 30s break duration to move to half-open state
        timeProvider.Advance(TimeSpan.FromSeconds(31));

        // The next request should succeed because the circuit transitions to half-open,
        // the probe request succeeds (WireMock now returns 200), and the circuit closes.
        HttpClient recoveredClient = _httpClientFactory.CreateClient("KeycloakAdminClient");
        HttpResponseMessage response = await recoveredClient.GetAsync("/admin/realms/foundry/users");

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
    }
}
