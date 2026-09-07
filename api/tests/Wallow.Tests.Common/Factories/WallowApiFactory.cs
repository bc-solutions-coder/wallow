using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;
using Wallow.Identity.Application.Commands.BootstrapAdmin;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Application.Queries.IsSetupRequired;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Domain.Identity;
using Wallow.Identity.Infrastructure.Data;
using Wallow.Identity.Infrastructure.Persistence;
using Wallow.Shared.Contracts.ApiKeys;
using Wallow.Shared.Contracts.Identity;
using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.MultiTenancy;
using Wallow.Tests.Common.Fakes;
using Wallow.Tests.Common.Helpers;

namespace Wallow.Tests.Common.Factories;

public class WallowApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    // Give test hosts an unfrozen Serilog logger.
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
        // Replace the shared logger before starting this fixture.
        await Log.CloseAndFlushAsync();
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Warning()
            .WriteTo.Console()
            .CreateLogger();

        await Task.WhenAll(
            _postgres.StartAsync(),
            _redis.StartAsync());

        // Modules capture these settings during host creation, before the later configuration override.
        // Fixtures share process environment variables; consuming assemblies must disable collection parallelization.
        string redisConnection = _redis.GetConnectionString() + ",allowAdmin=true";
        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", _postgres.GetConnectionString());
        Environment.SetEnvironmentVariable("ConnectionStrings__Redis", redisConnection);

        // The Testing environment uses configured OpenIddict certificates.
        const string certPassword = "test";
        _signingCertPath = GenerateEphemeralCert("CN=WallowTestSigning", certPassword);
        _encryptionCertPath = GenerateEphemeralCert("CN=WallowTestEncryption", certPassword);
        Environment.SetEnvironmentVariable("OpenIddict__SigningCertPath", _signingCertPath);
        Environment.SetEnvironmentVariable("OpenIddict__SigningCertPassword", certPassword);
        Environment.SetEnvironmentVariable("OpenIddict__EncryptionCertPath", _encryptionCertPath);
        Environment.SetEnvironmentVariable("OpenIddict__EncryptionCertPassword", certPassword);

        // Set these before host creation as well. Short leeway keeps replay tests fast;
        // a five-day fallback distinguishes it from an explicit seven-day client setting.
        Environment.SetEnvironmentVariable("OpenIddict__RefreshTokenReuseLeewaySeconds", "2");
        Environment.SetEnvironmentVariable("OpenIddict__RefreshTokenLifetimeDays", "5");
    }

    // Allow derived fixtures to extend cleanup through IAsyncLifetime.
    public new virtual async Task DisposeAsync()
    {
        Console.WriteLine("[WallowApiFactory] DisposeAsync called");

        // Stop background services before removing their containers.
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


        try
        {
            await base.DisposeAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[WallowApiFactory] Base dispose error: {ex.Message}");
        }


        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", null);
        Environment.SetEnvironmentVariable("ConnectionStrings__Redis", null);
        Environment.SetEnvironmentVariable("OpenIddict__SigningCertPath", null);
        Environment.SetEnvironmentVariable("OpenIddict__SigningCertPassword", null);
        Environment.SetEnvironmentVariable("OpenIddict__EncryptionCertPath", null);
        Environment.SetEnvironmentVariable("OpenIddict__EncryptionCertPassword", null);
        Environment.SetEnvironmentVariable("OpenIddict__RefreshTokenReuseLeewaySeconds", null);
        Environment.SetEnvironmentVariable("OpenIddict__RefreshTokenLifetimeDays", null);


        DeleteFileSafely(_signingCertPath);
        DeleteFileSafely(_encryptionCertPath);


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
                ["OpenIddict:SigningCertPath"] = _signingCertPath,
                ["OpenIddict:SigningCertPassword"] = "test",
                ["OpenIddict:EncryptionCertPath"] = _encryptionCertPath,
                ["OpenIddict:EncryptionCertPassword"] = "test",
                // Bootstrap an admin user so SetupMiddleware does not block test requests with 503
                ["AdminBootstrap:Email"] = "admin@wallow.test",
                ["AdminBootstrap:Password"] = "Admin1234!",
                ["AdminBootstrap:FirstName"] = "Test",
                ["AdminBootstrap:LastName"] = "Admin",
                // Use a host-only cookie for localhost test requests.
                ["Authentication:CookieDomain"] = string.Empty,
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

            // Keep user search independent of database contents.
            services.AddSingleton<IUserQueryService, FakeUserQueryService>();

            // The test host runs setup seeding without the separate seeder process.
            services.AddScoped<ApiScopeSeeder>();
            services.AddHostedService<TestSeedHostedService>();
        });
    }

    /// <summary>
    /// Seeds roles and scopes, then bootstraps an admin when setup is required.
    /// </summary>
    private sealed class TestSeedHostedService(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration) : IHostedService
    {
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
            IServiceProvider sp = scope.ServiceProvider;

            DefaultRoleSeeder roleSeeder = sp.GetRequiredService<DefaultRoleSeeder>();
            await roleSeeder.SeedAsync();

            // Populate the catalog used to validate client scopes.
            ApiScopeSeeder scopeSeeder = sp.GetRequiredService<ApiScopeSeeder>();
            await scopeSeeder.SeedAsync(sp.GetRequiredService<IdentityDbContext>(), cancellationToken);

            string? email = configuration["AdminBootstrap:Email"];
            string? password = configuration["AdminBootstrap:Password"];
            string? firstName = configuration["AdminBootstrap:FirstName"];
            string? lastName = configuration["AdminBootstrap:LastName"];

            if (!string.IsNullOrWhiteSpace(email) && !string.IsNullOrWhiteSpace(password))
            {
                ISetupStatusChecker setupStatusChecker = sp.GetRequiredService<ISetupStatusChecker>();
                bool setupRequired = await setupStatusChecker.IsSetupRequiredAsync(cancellationToken);

                if (setupRequired)
                {
                    IBootstrapAdminService bootstrapAdminService = sp.GetRequiredService<IBootstrapAdminService>();
                    await bootstrapAdminService.EnsureRoleExistsAsync("admin", cancellationToken);
                    bool userExists = await bootstrapAdminService.UserExistsAsync(email, cancellationToken);
                    if (!userExists)
                    {
                        await bootstrapAdminService.CreateUserAsync(
                            email,
                            password,
                            firstName ?? string.Empty,
                            lastName ?? string.Empty,
                            cancellationToken);
                    }

                    await EnrollTestAdminAsync(sp, cancellationToken);
                }
            }
        }

        /// <summary>
        /// Adds an admin membership for the synthetic TestAuthHandler identity if absent.
        /// This identity differs from the user created by admin bootstrap.
        /// </summary>
        private static async Task EnrollTestAdminAsync(IServiceProvider sp, CancellationToken ct)
        {
            RoleManager<WallowRole> roleManager = sp.GetRequiredService<RoleManager<WallowRole>>();
            WallowRole? adminRole = await roleManager.FindByNameAsync("admin");
            if (adminRole is null)
            {
                return;
            }

            IMembershipRepository memberships = sp.GetRequiredService<IMembershipRepository>();
            Membership? existing = await memberships.GetAsync(TestConstants.AdminUserId, TestConstants.TestOrgId, ct);
            if (existing is not null)
            {
                return;
            }

            memberships.Add(Membership.Enroll(
                TestConstants.AdminUserId,
                OrganizationId.Create(TestConstants.TestOrgId),
                adminRole.Id,
                sp.GetRequiredService<TimeProvider>()));

            await memberships.SaveChangesAsync(ct);
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
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
