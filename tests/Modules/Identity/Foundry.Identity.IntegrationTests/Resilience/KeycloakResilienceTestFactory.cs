using Foundry.Tests.Common.Factories;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Time.Testing;
using Polly;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Foundry.Identity.IntegrationTests.Resilience;

public class KeycloakResilienceTestFactory : FoundryApiFactory
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

        // Point Keycloak config to WireMock so OIDC discovery doesn't hit real infrastructure
        builder.UseSetting("Keycloak:auth-server-url", _wireMock.Url!);

        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<TimeProvider>(_fakeTimeProvider);

            // Re-register with WireMock base address. Clear existing message handlers
            // (including the production resilience handler) and add our own with
            // zero retry delays so FakeTimeProvider doesn't cause hangs.
            services.AddHttpClient("KeycloakAdminClient", client =>
            {
                client.BaseAddress = new Uri(_wireMock.Url!);
            })
            .ConfigureAdditionalHttpMessageHandlers((handlers, _) => handlers.Clear())
            .AddStandardResilienceHandler(options =>
            {
                // Match production circuit breaker settings exactly
                options.CircuitBreaker.FailureRatio = 0.5;
                options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
                options.CircuitBreaker.MinimumThroughput = 10;
                options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(30);

                // Zero retry delays so FakeTimeProvider doesn't hang waiting
                options.Retry.MaxRetryAttempts = 3;
                options.Retry.Delay = TimeSpan.Zero;
                options.Retry.BackoffType = DelayBackoffType.Constant;
                options.Retry.UseJitter = false;

                // Generous timeouts so they don't interfere
                options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(60);
                options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(10);
            });

            // Remove all health checks so the app starts without infrastructure
            services.Configure<HealthCheckServiceOptions>(options =>
            {
                options.Registrations.Clear();
            });
        });
    }

    public void SetupAllEndpointsReturn500()
    {
        _wireMock!.Reset();

        SetupOidcDiscoveryStub();

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

        SetupOidcDiscoveryStub();

        _wireMock
            .Given(Request.Create()
                .WithPath("/admin/realms/*")
                .UsingAnyMethod())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("[]"));
    }

    private void SetupOidcDiscoveryStub()
    {
        // Stub OIDC discovery so startup auth configuration doesn't hang
        _wireMock!
            .Given(Request.Create()
                .WithPath("*openid-configuration*")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody($$"""
                    {
                        "issuer": "{{_wireMock.Url}}/realms/foundry",
                        "authorization_endpoint": "{{_wireMock.Url}}/realms/foundry/protocol/openid-connect/auth",
                        "token_endpoint": "{{_wireMock.Url}}/realms/foundry/protocol/openid-connect/token",
                        "jwks_uri": "{{_wireMock.Url}}/realms/foundry/protocol/openid-connect/certs",
                        "subject_types_supported": ["public"],
                        "id_token_signing_alg_values_supported": ["RS256"],
                        "response_types_supported": ["code"]
                    }
                    """));

        // Stub JWKS endpoint
        _wireMock
            .Given(Request.Create()
                .WithPath("*/protocol/openid-connect/certs")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""{"keys": []}"""));
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        _wireMock?.Stop();
        _wireMock?.Dispose();
    }
}
