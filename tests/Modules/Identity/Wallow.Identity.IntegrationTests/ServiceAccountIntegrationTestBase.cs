using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Infrastructure.Data;
using Wallow.Identity.Infrastructure.Persistence;
using Wallow.Identity.IntegrationTests.Fakes;
using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.MultiTenancy;
using Wallow.Tests.Common.Bases;
using Wallow.Tests.Common.Factories;
using Wallow.Tests.Common.Helpers;

namespace Wallow.Identity.IntegrationTests;

[CollectionDefinition("ServiceAccounts")]
public class ServiceAccountTestCollection : ICollectionFixture<ServiceAccountTestFactory>;

[Collection("ServiceAccounts")]
[Trait("Category", "Integration")]
public class ServiceAccountIntegrationTestBase(ServiceAccountTestFactory factory) : WallowIntegrationTestBase(factory)
{
    protected IServiceAccountService ServiceAccountService { get; set; } = null!;
    protected IApiScopeRepository ApiScopeRepository { get; set; } = null!;

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();

        ServiceAccountService = ScopedServices.GetRequiredService<IServiceAccountService>();
        ApiScopeRepository = ScopedServices.GetRequiredService<IApiScopeRepository>();

        IdentityDbContext context = ScopedServices.GetRequiredService<IdentityDbContext>();
        await context.Database.EnsureCreatedAsync();

        // Clean up data before each test
        await CleanupDatabaseAsync(context);

        ILogger<ApiScopeSeeder> logger = ScopedServices.GetRequiredService<ILogger<ApiScopeSeeder>>();
        ApiScopeSeeder seeder = new(logger);

        // Seed is idempotent, but we need to detach any tracked entities first
        // to avoid duplicate key errors when seeding is called multiple times
        context.ChangeTracker.Clear();
        await seeder.SeedAsync(context);
    }

    private static async Task CleanupDatabaseAsync(IdentityDbContext context)
    {
        await context.ScimSyncLogs.ExecuteDeleteAsync();
        await context.ScimConfigurations.ExecuteDeleteAsync();
        await context.SsoConfigurations.ExecuteDeleteAsync();
        await context.ServiceAccountMetadata.ExecuteDeleteAsync();
    }
}

public class ServiceAccountTestFactory : WallowApiFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureTestServices(services =>
        {
            services.AddScoped<IServiceAccountService, FakeServiceAccountService>();

            // Replace the fixed tenant context with one that reads from X-Tenant-Id header
            services.AddScoped<ITenantContext>(sp =>
            {
                Microsoft.AspNetCore.Http.IHttpContextAccessor? httpContextAccessor = sp.GetService<Microsoft.AspNetCore.Http.IHttpContextAccessor>();
                Microsoft.AspNetCore.Http.HttpContext? context = httpContextAccessor?.HttpContext;

                if (context?.Request.Headers.TryGetValue("X-Tenant-Id", out Microsoft.Extensions.Primitives.StringValues tenantIdHeader) == true
                    && Guid.TryParse(tenantIdHeader, out Guid tenantGuid))
                {
                    TenantContext tenantCtx = new();
                    tenantCtx.SetTenant(TenantId.Create(tenantGuid), $"Test Tenant {tenantGuid}");
                    return tenantCtx;
                }

                TenantContext defaultCtx = new();
                defaultCtx.SetTenant(TenantId.Create(TestConstants.TestTenantId), "Test Tenant");
                return defaultCtx;
            });
        });
    }
}
