using System.Security.Claims;
using Foundry.Shared.Infrastructure.Auditing;
using Foundry.Shared.Kernel.Identity;
using Foundry.Shared.Kernel.MultiTenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Testcontainers.PostgreSql;

namespace Foundry.Shared.Infrastructure.Tests.Auditing;

public class TenantIsolationTestEntity
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
}

public class TenantIsolationTestDbContext : DbContext
{
    public DbSet<TenantIsolationTestEntity> TestEntities => Set<TenantIsolationTestEntity>();

    public TenantIsolationTestDbContext(DbContextOptions<TenantIsolationTestDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("tenant_iso_test");
        modelBuilder.Entity<TenantIsolationTestEntity>(e =>
        {
            e.ToTable("test_entities");
            e.HasKey(x => x.Id);
        });
    }
}

[Trait("Category", "Integration")]
public class AuditTenantIsolationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18-alpine")
        .WithCleanUp(true)
        .Build();

    private readonly ITenantContext _tenantContext = Substitute.For<ITenantContext>();
    private readonly IHttpContextAccessor _httpContextAccessor = Substitute.For<IHttpContextAccessor>();

    private ServiceProvider _serviceProvider = null!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        ServiceCollection services = new();

        services.AddSingleton(_tenantContext);
        services.AddSingleton(_httpContextAccessor);

        services.AddDbContext<AuditDbContext>(opts =>
            opts.UseNpgsql(_postgres.GetConnectionString()));

        services.AddLogging();
        services.AddSingleton<AuditInterceptor>();

        services.AddDbContext<TenantIsolationTestDbContext>((sp, opts) =>
        {
            opts.UseNpgsql(_postgres.GetConnectionString());
            opts.AddInterceptors(sp.GetRequiredService<AuditInterceptor>());
        });

        _serviceProvider = services.BuildServiceProvider();

        using IServiceScope scope = _serviceProvider.CreateScope();
        AuditDbContext auditDb = scope.ServiceProvider.GetRequiredService<AuditDbContext>();
        await auditDb.Database.EnsureCreatedAsync();

        TenantIsolationTestDbContext testDb = scope.ServiceProvider.GetRequiredService<TenantIsolationTestDbContext>();
        Microsoft.EntityFrameworkCore.Storage.RelationalDatabaseCreator creator = (Microsoft.EntityFrameworkCore.Storage.RelationalDatabaseCreator)
            ((IInfrastructure<IServiceProvider>)testDb.Database).Instance
                .GetRequiredService<Microsoft.EntityFrameworkCore.Storage.IDatabaseCreator>();
        await creator.CreateTablesAsync();
    }

    public async Task DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    [Fact]
    public async Task SaveChanges_CapturesTenantIdFromTenantContext()
    {
        Guid tenantId = Guid.NewGuid();
        ConfigureTenant(tenantId);
        ConfigureUser("user-1");

        using IServiceScope scope = _serviceProvider.CreateScope();
        TenantIsolationTestDbContext db = scope.ServiceProvider.GetRequiredService<TenantIsolationTestDbContext>();

        db.TestEntities.Add(new TenantIsolationTestEntity { Id = Guid.NewGuid(), Name = "Tenant Test" });
        await db.SaveChangesAsync();

        AuditDbContext auditDb = scope.ServiceProvider.GetRequiredService<AuditDbContext>();
        List<AuditEntry> entries = await auditDb.AuditEntries.ToListAsync();

        entries.Should().ContainSingle();
        entries[0].TenantId.Should().Be(tenantId);
    }

    [Fact]
    public async Task SaveChanges_CapturesUserIdFromHttpContext()
    {
        Guid tenantId = Guid.NewGuid();
        ConfigureTenant(tenantId);
        ConfigureUser("user-42");

        using IServiceScope scope = _serviceProvider.CreateScope();
        TenantIsolationTestDbContext db = scope.ServiceProvider.GetRequiredService<TenantIsolationTestDbContext>();

        db.TestEntities.Add(new TenantIsolationTestEntity { Id = Guid.NewGuid(), Name = "User Test" });
        await db.SaveChangesAsync();

        AuditDbContext auditDb = scope.ServiceProvider.GetRequiredService<AuditDbContext>();
        List<AuditEntry> entries = await auditDb.AuditEntries.ToListAsync();

        entries.Should().ContainSingle();
        entries[0].UserId.Should().Be("user-42");
    }

    [Fact]
    public async Task SaveChanges_DifferentTenants_AuditEntriesIsolatedByTenantId()
    {
        Guid tenantA = Guid.NewGuid();
        Guid tenantB = Guid.NewGuid();

        // Insert entity under tenant A
        ConfigureTenant(tenantA);
        ConfigureUser("user-a");

        using (IServiceScope scopeA = _serviceProvider.CreateScope())
        {
            TenantIsolationTestDbContext dbA = scopeA.ServiceProvider.GetRequiredService<TenantIsolationTestDbContext>();
            dbA.TestEntities.Add(new TenantIsolationTestEntity { Id = Guid.NewGuid(), Name = "Entity A" });
            await dbA.SaveChangesAsync();
        }

        // Insert entity under tenant B
        ConfigureTenant(tenantB);
        ConfigureUser("user-b");

        using (IServiceScope scopeB = _serviceProvider.CreateScope())
        {
            TenantIsolationTestDbContext dbB = scopeB.ServiceProvider.GetRequiredService<TenantIsolationTestDbContext>();
            dbB.TestEntities.Add(new TenantIsolationTestEntity { Id = Guid.NewGuid(), Name = "Entity B" });
            await dbB.SaveChangesAsync();
        }

        // Query audit entries and verify isolation
        using IServiceScope queryScope = _serviceProvider.CreateScope();
        AuditDbContext auditDb = queryScope.ServiceProvider.GetRequiredService<AuditDbContext>();

        List<AuditEntry> tenantAEntries = await auditDb.AuditEntries
            .Where(e => e.TenantId == tenantA).ToListAsync();
        List<AuditEntry> tenantBEntries = await auditDb.AuditEntries
            .Where(e => e.TenantId == tenantB).ToListAsync();

        tenantAEntries.Should().ContainSingle();
        tenantAEntries[0].UserId.Should().Be("user-a");

        tenantBEntries.Should().ContainSingle();
        tenantBEntries[0].UserId.Should().Be("user-b");
    }

    private void ConfigureTenant(Guid tenantId)
    {
        _tenantContext.IsResolved.Returns(true);
        _tenantContext.TenantId.Returns(TenantId.Create(tenantId));
    }

    private void ConfigureUser(string userId)
    {
        DefaultHttpContext httpContext = new();
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("sub", userId)], "test"));
        _httpContextAccessor.HttpContext.Returns(httpContext);
    }
}
