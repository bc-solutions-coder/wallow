using Wallow.Identity.Application.Interfaces;
using Wallow.Shared.Contracts.Billing;
using Wallow.Shared.Contracts.Identity;
using Wallow.Shared.Contracts.Metering;
using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.MultiTenancy;
using Wallow.Tests.Common.Fakes;
using Wallow.Tests.Common.Helpers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using Testcontainers.Redis;

namespace Wallow.Tests.Common.Factories;

public class WallowApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    // Reset Serilog's static logger to avoid "logger is already frozen" error
    // when multiple test classes create their own WebApplicationFactory
    static WallowApiFactory()
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Warning()
            .WriteTo.Console()
            .CreateLogger();
    }
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18-alpine")
        .WithDatabase("wallow_test")
        .WithUsername("test")
        .WithPassword("test")
        .WithCleanUp(true)
        .Build();

    private readonly RabbitMqContainer _rabbitMq = new RabbitMqBuilder("rabbitmq:4.2-management-alpine")
        .WithCleanUp(true)
        .Build();

    private readonly RedisContainer _redis = new RedisBuilder("valkey/valkey:8-alpine")
        .WithCleanUp(true)
        .Build();

    public async Task InitializeAsync()
    {
        // Reset Serilog before each test run to avoid "logger is already frozen" error
        await Log.CloseAndFlushAsync();
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Warning()
            .WriteTo.Console()
            .CreateLogger();

        await Task.WhenAll(
            _postgres.StartAsync(),
            _rabbitMq.StartAsync(),
            _redis.StartAsync());

        // Set environment variables so connection strings are available BEFORE
        // WebApplication.CreateBuilder() runs in Program.cs. Modules capture connection
        // strings at service registration time (before ConfigureAppConfiguration applies),
        // so the in-memory override in ConfigureWebHost is too late. Environment variables
        // are read by the default EnvironmentVariablesConfigurationProvider during builder
        // creation, making them visible when modules call configuration.GetConnectionString().
        string redisConnection = _redis.GetConnectionString() + ",allowAdmin=true";
        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", _postgres.GetConnectionString());
        Environment.SetEnvironmentVariable("ConnectionStrings__RabbitMq", _rabbitMq.GetConnectionString());
        Environment.SetEnvironmentVariable("ConnectionStrings__Redis", redisConnection);
        Environment.SetEnvironmentVariable("Elsa__Identity__SigningKey", "wallow-test-elsa-signing-key-for-testing-only");
    }

    public new async Task DisposeAsync()
    {
        Console.WriteLine("[WallowApiFactory] DisposeAsync called");

        // Stop the host gracefully to allow background services (e.g., Wolverine, Elsa)
        // to shut down before containers are disposed
        try
        {
            IHost? host = Services.GetService<IHost>();
            if (host is not null)
            {
                using CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                await host.StopAsync(cts.Token);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[WallowApiFactory] Host stop error: {ex.Message}");
        }

        // Dispose the WebApplicationFactory (which disposes the host)
        try
        {
            await base.DisposeAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[WallowApiFactory] Base dispose error: {ex.Message}");
        }

        // Give Wolverine/RabbitMQ a moment to finish closing channels
        await Task.Delay(100);

        // Clear environment variables set in InitializeAsync
        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", null);
        Environment.SetEnvironmentVariable("ConnectionStrings__RabbitMq", null);
        Environment.SetEnvironmentVariable("ConnectionStrings__Redis", null);
        Environment.SetEnvironmentVariable("Elsa__Identity__SigningKey", null);

        // Dispose containers to prevent accumulation
        Console.WriteLine("[WallowApiFactory] Disposing containers...");
        await DisposeContainerSafelyAsync(_postgres, "postgres");
        await DisposeContainerSafelyAsync(_rabbitMq, "rabbitmq");
        await DisposeContainerSafelyAsync(_redis, "redis");
        Console.WriteLine("[WallowApiFactory] Containers disposed");
    }

    private static async Task DisposeContainerSafelyAsync(IAsyncDisposable container, string name)
    {
        try
        {
            await container.DisposeAsync();
            Console.WriteLine($"[WallowApiFactory] {name} disposed successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[WallowApiFactory] {name} dispose error: {ex.Message}");
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            // Add allowAdmin=true for Redis to enable FLUSHDB in tests
            string redisConnection = _redis.GetConnectionString() + ",allowAdmin=true";
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = _postgres.GetConnectionString(),
                ["ConnectionStrings:RabbitMq"] = _rabbitMq.GetConnectionString(),
                ["ConnectionStrings:Redis"] = redisConnection,
                ["Elsa:Identity:SigningKey"] = "wallow-test-elsa-signing-key-for-testing-only",
            });
        });

        builder.ConfigureTestServices(services =>
        {
            services.AddAuthentication("Test")
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", _ => { });

            services.AddScoped<ITenantContext>(sp =>
            {
                IHttpContextAccessor httpContextAccessor = sp.GetRequiredService<IHttpContextAccessor>();
                string? tenantHeader = httpContextAccessor.HttpContext?.Request.Headers["X-Test-Tenant-Id"].FirstOrDefault();
                Guid tenantId = !string.IsNullOrEmpty(tenantHeader) && Guid.TryParse(tenantHeader, out Guid parsed)
                    ? parsed
                    : TestConstants.TestTenantId;

                return new TenantContext
                {
                    TenantId = TenantId.Create(tenantId),
                    TenantName = "Test Tenant",
                    IsResolved = true
                };
            });

            services.AddSingleton<IUserManagementService, FakeUserManagementService>();

            // Replace real query services that depend on external systems (Keycloak, Marten, raw DB)
            // with fakes so integration tests don't require those systems to be fully initialised.
            services.AddSingleton<IInvoiceQueryService, FakeInvoiceQueryService>();
            services.AddSingleton<IUserQueryService, FakeUserQueryService>();
            services.AddSingleton<IMeteringQueryService, FakeMeteringQueryService>();
        });
    }
}
