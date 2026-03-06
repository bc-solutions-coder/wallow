using Foundry.Identity.Application.Interfaces;
using Foundry.Identity.Infrastructure.Data;
using Foundry.Identity.Infrastructure.Persistence;
using Foundry.Identity.IntegrationTests.Fakes;
using Foundry.Shared.Kernel.Identity;
using Foundry.Shared.Kernel.MultiTenancy;
using Foundry.Tests.Common.Bases;
using Foundry.Tests.Common.Factories;
using Foundry.Tests.Common.Helpers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Foundry.Identity.IntegrationTests;

[Trait("Category", "Integration")]
public class ServiceAccountIntegrationTestBase : FoundryIntegrationTestBase, IClassFixture<ServiceAccountTestFactory>
{
    protected IServiceAccountService ServiceAccountService { get; set; } = null!;
    protected IApiScopeRepository ApiScopeRepository { get; set; } = null!;

    public ServiceAccountIntegrationTestBase(ServiceAccountTestFactory factory) : base(factory) { }

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
        context.ScimSyncLogs.RemoveRange(context.ScimSyncLogs);
        context.ScimConfigurations.RemoveRange(context.ScimConfigurations);
        context.SsoConfigurations.RemoveRange(context.SsoConfigurations);
        context.ServiceAccountMetadata.RemoveRange(context.ServiceAccountMetadata);
        await context.SaveChangesAsync();
    }
}

public class ServiceAccountTestFactory : FoundryApiFactory
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
