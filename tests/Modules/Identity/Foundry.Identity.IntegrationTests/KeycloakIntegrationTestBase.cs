using Foundry.Tests.Common.Fixtures;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Serilog;

namespace Foundry.Identity.IntegrationTests;

/// <summary>
/// Collection definition that ensures all Keycloak-based test classes share
/// a single KeycloakTestFixture instance (and its 4 containers).
/// This prevents configuration conflicts between parallel test classes.
/// </summary>
[CollectionDefinition(Name)]
public class KeycloakTestCollection : ICollectionFixture<KeycloakTestFixture>
{
    public const string Name = "Keycloak";
}

/// <summary>
/// Base class for integration tests that use a real Keycloak instance.
/// Provides OAuth2 token acquisition and validation with real JWT tokens.
/// </summary>
[Collection(KeycloakTestCollection.Name)]
[Trait("Category", "Integration")]
public class KeycloakIntegrationTestBase(KeycloakTestFixture fixture) : IAsyncLifetime
{
    protected KeycloakTestFixture Fixture { get; } = fixture;
    protected HttpClient Client { get; } = fixture.CreateClient();

    public virtual Task InitializeAsync() => Task.CompletedTask;

    public virtual Task DisposeAsync() => Task.CompletedTask;

    protected async Task<string> GetServiceAccountTokenAsync(string clientId, string clientSecret)
    {
        return await Fixture.KeycloakFixture.GetServiceAccountTokenAsync(clientId, clientSecret);
    }

    protected void SetAuthorizationHeader(string token)
    {
        Client.DefaultRequestHeaders.Remove("Authorization");
        Client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
    }
}

/// <summary>
/// Test fixture that starts Keycloak and configures the API to use it for real authentication.
/// </summary>
public class KeycloakTestFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    public KeycloakFixture KeycloakFixture { get; } = new();
    private readonly DatabaseFixture _databaseFixture = new();
    private readonly RabbitMqFixture _rabbitMqFixture = new();
    private readonly RedisFixture _redisFixture = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = _databaseFixture.ConnectionString,
                ["ConnectionStrings:RabbitMq"] = _rabbitMqFixture.ConnectionString,
                ["ConnectionStrings:Redis"] = _redisFixture.ConnectionString + ",allowAdmin=true",
                ["Keycloak:Authority"] = $"{KeycloakFixture.BaseUrl}/realms/{KeycloakFixture.RealmName}",
                ["Keycloak:Audience"] = KeycloakFixture.ClientId,
                ["Keycloak:AdminUrl"] = KeycloakFixture.BaseUrl,
                ["Keycloak:AdminUsername"] = KeycloakFixture.AdminUsername,
                ["Keycloak:AdminPassword"] = KeycloakFixture.AdminPassword,
                ["Keycloak:Realm"] = KeycloakFixture.RealmName,
                ["Elsa:Identity:SigningKey"] = "foundry-test-elsa-signing-key-for-testing-only"
            });
        });

        builder.ConfigureTestServices(services =>
        {
            // Remove Elsa's background hosted services (e.g. BookmarkQueuePurger, ScheduledTimer)
            // to prevent ObjectDisposedException when timer callbacks fire after the service
            // provider is disposed during test cleanup.
            List<ServiceDescriptor> elsaHostedServices = services
                .Where(d => d.ServiceType == typeof(IHostedService)
                    && (d.ImplementationType?.Namespace?.StartsWith("Elsa", StringComparison.Ordinal) == true
                        || d.ImplementationFactory?.Method.DeclaringType?.Namespace?.StartsWith("Elsa", StringComparison.Ordinal) == true))
                .ToList();
            foreach (ServiceDescriptor descriptor in elsaHostedServices)
            {
                services.Remove(descriptor);
            }

            // Reconfigure the existing JWT Bearer scheme (registered by AddKeycloakWebApiAuthentication
            // in production) to point at the real Keycloak test container.
            services.Configure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                options.Authority = $"{KeycloakFixture.BaseUrl}/realms/{KeycloakFixture.RealmName}";
                options.Audience = KeycloakFixture.ClientId;
                options.RequireHttpsMetadata = false;
#pragma warning disable CA5404 // Keycloak doesn't include audience by default in test env
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = false,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true
                };
#pragma warning restore CA5404
            });
        });
    }

    public async Task InitializeAsync()
    {
        // Reset Serilog before test run to avoid "logger is already frozen" error
        await Log.CloseAndFlushAsync();
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Warning()
            .WriteTo.Console()
            .CreateLogger();

        await Task.WhenAll(
            KeycloakFixture.InitializeAsync(),
            _databaseFixture.InitializeAsync(),
            _rabbitMqFixture.InitializeAsync(),
            _redisFixture.InitializeAsync());

        // Set environment variables so connection strings are available BEFORE
        // WebApplication.CreateBuilder() runs. Modules capture connection strings
        // at service registration time (before ConfigureAppConfiguration applies).
        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", _databaseFixture.ConnectionString);
        Environment.SetEnvironmentVariable("ConnectionStrings__RabbitMq", _rabbitMqFixture.ConnectionString);
        Environment.SetEnvironmentVariable("ConnectionStrings__Redis", _redisFixture.ConnectionString + ",allowAdmin=true");
        Environment.SetEnvironmentVariable("Elsa__Identity__SigningKey", "foundry-test-elsa-signing-key-for-testing-only");
    }

    public new async Task DisposeAsync()
    {
        // Clear environment variables to avoid leaking into other test runs
        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", null);
        Environment.SetEnvironmentVariable("ConnectionStrings__RabbitMq", null);
        Environment.SetEnvironmentVariable("ConnectionStrings__Redis", null);
        Environment.SetEnvironmentVariable("Elsa__Identity__SigningKey", null);

        // Stop the host gracefully before disposing containers
        try
        {
            IHost? host = Services.GetService<IHost>();
            if (host is not null)
            {
                using CancellationTokenSource cts = new(TimeSpan.FromSeconds(30));
                await host.StopAsync(cts.Token);
            }
        }
        catch { /* ignore shutdown errors */ }

        try { await base.DisposeAsync(); } catch { /* ignore */ }

        await Task.Delay(100);

        await KeycloakFixture.DisposeAsync();
        await _databaseFixture.DisposeAsync();
        await _rabbitMqFixture.DisposeAsync();
        await _redisFixture.DisposeAsync();
    }
}
