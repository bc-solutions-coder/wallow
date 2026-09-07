using Microsoft.EntityFrameworkCore;
using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.MultiTenancy;
using Wallow.Tests.Common.Fixtures;

namespace Wallow.Tests.Common.Bases;

/// <summary>
/// Creates a context with a fresh tenant ID using a shared PostgreSQL fixture.
/// Derived tests select the collection fixture and may customize options or context construction.
/// </summary>
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
    /// Uses EF migrations when true; otherwise initializes the schema with EnsureCreatedAsync.
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
    /// Configures database options. The base adds TenantSaveChangesInterceptor afterward.
    /// </summary>
    protected virtual DbContextOptionsBuilder<TDbContext> ConfigureOptions(
        DbContextOptionsBuilder<TDbContext> builder, string connectionString)
    {
        return builder.UseNpgsql(connectionString);
    }

    /// <summary>
    /// Constructs the context from typed options. Override for other constructor signatures.
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
