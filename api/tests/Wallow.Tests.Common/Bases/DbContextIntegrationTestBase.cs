using Microsoft.EntityFrameworkCore;
using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.MultiTenancy;
using Wallow.Tests.Common.Fixtures;

namespace Wallow.Tests.Common.Bases;

/// <summary>
/// Base class for DbContext integration tests that share a PostgreSQL container
/// via collection fixture. Creates a fresh DbContext per test class with tenant isolation.
/// </summary>
/// <remarks>
/// Usage:
/// 1. Define a collection: [CollectionDefinition("PostgresDatabase")] class DbCollection : ICollectionFixture&lt;PostgresContainerFixture&gt; { }
/// 2. Inherit: [Collection("PostgresDatabase")] class MyTests : DbContextIntegrationTestBase&lt;MyDbContext&gt; { ... }
/// 3. Override ConfigureOptions if needed (e.g., for NpgsqlDataSource with EnableDynamicJson).
/// 4. Override UseMigrateAsync to true for modules with EF migrations and schemas.
/// </remarks>
[Trait("Category", "Integration")]
public abstract class DbContextIntegrationTestBase<TDbContext> : IAsyncLifetime
    where TDbContext : DbContext
{
    private readonly PostgresContainerFixture _fixture;

    protected TDbContext DbContext { get; private set; } = null!;
    protected TenantContext TenantContext { get; private set; } = null!;
    protected TenantId TestTenantId { get; private set; }
    protected Guid TestUserId { get; } = Guid.NewGuid();
    protected string ConnectionString => _fixture.ConnectionString;

    /// <summary>
    /// When true, uses MigrateAsync() instead of EnsureCreatedAsync().
    /// Override to true for modules with EF migrations and schemas (e.g., Dapper query tests).
    /// </summary>
    protected virtual bool UseMigrateAsync => false;

    protected DbContextIntegrationTestBase(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    public virtual async Task InitializeAsync()
    {
        TenantContext = new TenantContext();
        TestTenantId = TenantId.New();
        TenantContext.SetTenant(TestTenantId, "TestTenant");

        DbContext = BuildDbContext(TenantContext);

        if (UseMigrateAsync)
        {
            await DbContext.Database.MigrateAsync();
        }
        else
        {
            await DbContext.Database.EnsureCreatedAsync();
        }
    }

    /// <summary>
    /// Configures DbContextOptions. Override to customize (e.g., add NpgsqlDataSource with EnableDynamicJson).
    /// Do NOT add TenantSaveChangesInterceptor here — it is added automatically.
    /// </summary>
    protected virtual DbContextOptionsBuilder<TDbContext> ConfigureOptions(
        DbContextOptionsBuilder<TDbContext> builder, string connectionString)
    {
        return builder.UseNpgsql(connectionString);
    }

    /// <summary>
    /// Creates the DbContext instance. Override if your DbContext has a non-standard constructor.
    /// Default assumes constructor(DbContextOptions&lt;TDbContext&gt;) with SetTenant called after.
    /// </summary>
    protected virtual TDbContext CreateDbContext(DbContextOptions<TDbContext> options, ITenantContext tenantContext)
    {
        return (TDbContext)Activator.CreateInstance(typeof(TDbContext), options)!;
    }

    protected TDbContext CreateDbContextForTenant(TenantId tenantId, string tenantName = "OtherTenant")
    {
        TenantContext otherContext = new TenantContext();
        otherContext.SetTenant(tenantId, tenantName);
        return BuildDbContext(otherContext);
    }

    private TDbContext BuildDbContext(TenantContext tenantContext)
    {
        DbContextOptionsBuilder<TDbContext> builder = ConfigureOptions(
            new DbContextOptionsBuilder<TDbContext>(),
            _fixture.ConnectionString);

        builder.AddInterceptors(new TenantSaveChangesInterceptor(tenantContext));

        TDbContext ctx = CreateDbContext(builder.Options, tenantContext);

        // Call SetTenant via reflection since TDbContext constraint is just DbContext
        System.Reflection.MethodInfo? setTenantMethod = ctx.GetType().GetMethod("SetTenant");
        setTenantMethod?.Invoke(ctx, [tenantContext.TenantId]);

        return ctx;
    }

    public virtual async Task DisposeAsync()
    {
        await DbContext.DisposeAsync();
    }
}
