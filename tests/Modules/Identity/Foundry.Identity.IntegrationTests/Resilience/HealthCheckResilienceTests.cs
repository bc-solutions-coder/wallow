using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace Foundry.Identity.IntegrationTests.Resilience;

[Trait("Category", "Integration")]
public class HealthCheckResilienceTests : IClassFixture<KeycloakResilienceTestFactory>, IAsyncLifetime
{
    private readonly KeycloakResilienceTestFactory _factory;
    private HttpClient _appClient = null!;

    public HealthCheckResilienceTests(KeycloakResilienceTestFactory factory)
    {
        _factory = factory;
    }

    public Task InitializeAsync()
    {
        HttpClient client = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Keycloak:auth-server-url", _factory.WireMock.Url!);
            builder.ConfigureTestServices(services =>
            {
                // Replace FakeTimeProvider with real TimeProvider so Polly retry
                // delays and timeouts work correctly in health check tests
                services.AddSingleton(TimeProvider.System);

                // Factory already keeps only the keycloak check; nothing more to configure here
            });
        }).CreateClient();

        _appClient = client;
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _appClient.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task HealthCheck_WithServerError_RetriesOnceAndReturnsUnhealthy()
    {
        _factory.WireMock.Reset();
        _factory.WireMock
            .Given(Request.Create().WithPath("*").UsingAnyMethod())
            .RespondWith(Response.Create()
                .WithStatusCode(500)
                .WithBody("Internal Server Error"));

        using CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        HttpResponseMessage response = await _appClient.GetAsync("/health", cts.Token);

        string keycloakStatus = await GetKeycloakEntryStatus(response);
        keycloakStatus.Should().Be("Unhealthy");

        List<WireMock.Logging.ILogEntry> entries = _factory.WireMock.LogEntries.ToList();
        int keycloakRequests = entries.Count(e =>
            e.RequestMessage.Path.Contains("openid-configuration", StringComparison.OrdinalIgnoreCase));
        keycloakRequests.Should().Be(2, "health-check profile retries once (1 original + 1 retry)");
    }

    [Fact]
    public async Task HealthCheck_WithDelayedResponse_CompletesWithinTotalTimeout()
    {
        _factory.WireMock.Reset();
        _factory.WireMock
            .Given(Request.Create().WithPath("*").UsingAnyMethod())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody("{}")
                .WithDelay(TimeSpan.FromSeconds(10)));

        Stopwatch stopwatch = Stopwatch.StartNew();
        HttpResponseMessage response = await _appClient.GetAsync("/health");
        stopwatch.Stop();

        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(15),
            "the health-check profile has a 5s TotalRequestTimeout");

        response.Should().NotBeNull();
    }

    [Fact]
    public async Task HealthCheck_WithHealthyKeycloak_ReturnsHealthy()
    {
        _factory.WireMock.Reset();
        _factory.WireMock
            .Given(Request.Create().WithPath("*").UsingAnyMethod())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("{}"));

        HttpResponseMessage response = await _appClient.GetAsync("/health");

        string keycloakStatus = await GetKeycloakEntryStatus(response);
        keycloakStatus.Should().Be("Healthy");
    }

    private static async Task<string> GetKeycloakEntryStatus(HttpResponseMessage response)
    {
        string body = await response.Content.ReadAsStringAsync();
        JsonDocument doc = JsonDocument.Parse(body);
        JsonElement checks = doc.RootElement.GetProperty("checks");
        foreach (JsonElement check in checks.EnumerateArray())
        {
            if (check.GetProperty("name").GetString() == "keycloak")
            {
                return check.GetProperty("status").GetString()!;
            }
        }

        throw new InvalidOperationException("keycloak health check entry not found in response");
    }
}
