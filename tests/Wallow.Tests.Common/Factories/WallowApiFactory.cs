using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Wallow.Identity.Application.Interfaces;
using Wallow.Shared.Contracts.ApiKeys;
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

    private readonly RedisContainer _redis = new RedisBuilder("valkey/valkey:8-alpine")
        .WithCleanUp(true)
        .Build();

    private string _signingCertPath = string.Empty;
    private string _encryptionCertPath = string.Empty;

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
            _redis.StartAsync());

        // Set environment variables so connection strings are available BEFORE
        // WebApplication.CreateBuilder() runs in Program.cs. Modules capture connection
        // strings at service registration time (before ConfigureAppConfiguration applies),
        // so the in-memory override in ConfigureWebHost is too late. Environment variables
        // are read by the default EnvironmentVariablesConfigurationProvider during builder
        // creation, making them visible when modules call configuration.GetConnectionString().
        string redisConnection = _redis.GetConnectionString() + ",allowAdmin=true";
        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", _postgres.GetConnectionString());
        Environment.SetEnvironmentVariable("ConnectionStrings__Redis", redisConnection);
        Environment.SetEnvironmentVariable("Elsa__Identity__SigningKey", "wallow-test-elsa-signing-key-for-testing-only");

        // Generate ephemeral self-signed certificates for OpenIddict so the
        // non-development code path in IdentityInfrastructureExtensions doesn't throw.
        const string certPassword = "test";
        _signingCertPath = GenerateEphemeralCert("CN=WallowTestSigning", certPassword);
        _encryptionCertPath = GenerateEphemeralCert("CN=WallowTestEncryption", certPassword);
        Environment.SetEnvironmentVariable("OpenIddict__SigningCertPath", _signingCertPath);
        Environment.SetEnvironmentVariable("OpenIddict__SigningCertPassword", certPassword);
        Environment.SetEnvironmentVariable("OpenIddict__EncryptionCertPath", _encryptionCertPath);
        Environment.SetEnvironmentVariable("OpenIddict__EncryptionCertPassword", certPassword);
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

        // Clear environment variables set in InitializeAsync
        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", null);
        Environment.SetEnvironmentVariable("ConnectionStrings__Redis", null);
        Environment.SetEnvironmentVariable("Elsa__Identity__SigningKey", null);
        Environment.SetEnvironmentVariable("OpenIddict__SigningCertPath", null);
        Environment.SetEnvironmentVariable("OpenIddict__SigningCertPassword", null);
        Environment.SetEnvironmentVariable("OpenIddict__EncryptionCertPath", null);
        Environment.SetEnvironmentVariable("OpenIddict__EncryptionCertPassword", null);

        // Clean up ephemeral certificate files
        DeleteFileSafely(_signingCertPath);
        DeleteFileSafely(_encryptionCertPath);

        // Dispose containers to prevent accumulation
        Console.WriteLine("[WallowApiFactory] Disposing containers...");
        await DisposeContainerSafelyAsync(_postgres, "postgres");
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

    private static string GenerateEphemeralCert(string subjectName, string password)
    {
        using RSA rsa = RSA.Create(2048);
        CertificateRequest request = new CertificateRequest(subjectName, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using X509Certificate2 cert = request.CreateSelfSigned(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddYears(1));
        string path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.pfx");
        File.WriteAllBytes(path, cert.Export(X509ContentType.Pfx, password));
        return path;
    }

    private static void DeleteFileSafely(string path)
    {
        try
        {
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // best effort cleanup
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
                ["ConnectionStrings:Redis"] = redisConnection,
                ["Elsa:Identity:SigningKey"] = "wallow-test-elsa-signing-key-for-testing-only",
                ["OpenIddict:SigningCertPath"] = _signingCertPath,
                ["OpenIddict:SigningCertPassword"] = "test",
                ["OpenIddict:EncryptionCertPath"] = _encryptionCertPath,
                ["OpenIddict:EncryptionCertPassword"] = "test",
                // Bootstrap an admin user so SetupMiddleware does not block test requests with 503
                ["AdminBootstrap:Email"] = "admin@wallow.test",
                ["AdminBootstrap:Password"] = "Admin1234!",
                ["AdminBootstrap:FirstName"] = "Test",
                ["AdminBootstrap:LastName"] = "Admin",
            });
        });

        builder.ConfigureTestServices(services =>
        {
            services.AddAuthentication(options =>
                {
                    options.DefaultScheme = "Test";
                    options.DefaultAuthenticateScheme = "Test";
                    options.DefaultChallengeScheme = "Test";
                })
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
            services.AddSingleton<IApiKeyService>(new FakeApiKeyService());

            // Replace real query services that depend on external systems (raw DB, etc.)
            // with fakes so integration tests don't require those systems to be fully initialised.
            services.AddSingleton<IInvoiceQueryService, FakeInvoiceQueryService>();
            services.AddSingleton<IUserQueryService, FakeUserQueryService>();
            services.AddSingleton<IMeteringQueryService, FakeMeteringQueryService>();
        });
    }

    private sealed class FakeApiKeyService : IApiKeyService
    {
        public Task<ApiKeyCreateResult> CreateApiKeyAsync(string name, Guid userId, Guid tenantId,
            IEnumerable<string>? scopes = null, DateTimeOffset? expiresAt = null, CancellationToken ct = default)
        {
            return Task.FromResult(new ApiKeyCreateResult(true, Guid.NewGuid().ToString(), "wallow_test_key", "wallow_t", null));
        }

        public Task<ApiKeyValidationResult> ValidateApiKeyAsync(string apiKey, CancellationToken ct = default)
        {
            return Task.FromResult(new ApiKeyValidationResult(false, null, null, null, null, "Fake service"));
        }

        public Task<IReadOnlyList<ApiKeyMetadata>> ListApiKeysAsync(Guid userId, Guid tenantId, CancellationToken ct = default)
        {
            return Task.FromResult<IReadOnlyList<ApiKeyMetadata>>(Array.Empty<ApiKeyMetadata>());
        }

        public Task<int> GetApiKeyCountAsync(Guid userId, CancellationToken ct = default)
        {
            return Task.FromResult(0);
        }

        public Task<bool> RevokeApiKeyAsync(string keyId, Guid userId, CancellationToken ct = default)
        {
            return Task.FromResult(false);
        }
    }
}
