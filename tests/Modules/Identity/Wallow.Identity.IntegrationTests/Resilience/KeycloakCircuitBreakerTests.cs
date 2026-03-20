using Microsoft.Extensions.DependencyInjection;

namespace Wallow.Identity.IntegrationTests.Resilience;

/// <summary>
/// Tests HTTP client resilience policies (circuit breaker behavior).
/// Uses WireMock to simulate upstream service failures.
/// </summary>
[Trait("Category", "Integration")]
public class IdentityResilienceCircuitBreakerTests : IClassFixture<IdentityResilienceTestFactory>, IAsyncLifetime
{
    private readonly IdentityResilienceTestFactory _factory;
    private IServiceScope? _scope;

    public IdentityResilienceCircuitBreakerTests(IdentityResilienceTestFactory factory)
    {
        _factory = factory;
    }

    public Task InitializeAsync()
    {
        _ = _factory.CreateClient();
        _scope = _factory.Services.CreateScope();
        _factory.SetupAllEndpointsReturn500();
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _scope?.Dispose();
        return Task.CompletedTask;
    }
}
