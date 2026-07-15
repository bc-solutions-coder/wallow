using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Time.Testing;
using Wallow.Tests.Common.Factories;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Wallow.Identity.IntegrationTests.Resilience;

public class IdentityResilienceTestFactory : WallowApiFactory
{
    private WireMockServer? _wireMock;
    private FakeTimeProvider? _fakeTimeProvider;

    public FakeTimeProvider TimeProvider => _fakeTimeProvider
        ?? throw new InvalidOperationException("Factory not initialized");

    public WireMockServer WireMock => _wireMock
        ?? throw new InvalidOperationException("Factory not initialized");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        _wireMock = WireMockServer.Start();
        _fakeTimeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);

        SetupAllEndpointsReturn500();

        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<TimeProvider>(_fakeTimeProvider);

            // Remove all health checks for resilience tests — we only test HTTP client behavior
            services.Configure<HealthCheckServiceOptions>(options =>
            {
                options.Registrations.Clear();
            });
        });
    }

    public void SetupAllEndpointsReturn500()
    {
        _wireMock!.Reset();

        _wireMock
            .Given(Request.Create()
                .WithPath("/admin/realms/*")
                .UsingAnyMethod())
            .RespondWith(Response.Create()
                .WithStatusCode(500)
                .WithBody("Internal Server Error"));
    }

    public void SetupAllEndpointsReturn200()
    {
        _wireMock!.Reset();

        _wireMock
            .Given(Request.Create()
                .WithPath("/admin/realms/*")
                .UsingAnyMethod())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("[]"));
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        _wireMock?.Stop();
        _wireMock?.Dispose();
    }
}

[CollectionDefinition("IdentityResilience")]
public class IdentityResilienceTestCollection : ICollectionFixture<IdentityResilienceTestFactory>
{
}
