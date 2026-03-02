using Foundry.Shared.Kernel.Identity;
using Foundry.Shared.Kernel.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace Foundry.Shared.Kernel.Tests.MultiTenancy;

public class TenantSaveChangesInterceptorTests
{
    private readonly ITenantContext _tenantContext;

    public TenantSaveChangesInterceptorTests()
    {
        _tenantContext = Substitute.For<ITenantContext>();
    }

    [Fact]
    public void SavingChanges_WithNewTenantScopedEntity_SetsTenantId()
    {
        TenantId tenantId = TenantId.New();
        _tenantContext.IsResolved.Returns(true);
        _tenantContext.TenantId.Returns(tenantId);
        using TestDbContext dbContext = CreateDbContext();
        dbContext.TenantEntities.Add(new TestTenantEntity { Name = "Test" });

        dbContext.SaveChanges();

        TestTenantEntity saved = dbContext.TenantEntities.Single();
        saved.TenantId.Should().Be(tenantId);
    }

    [Fact]
    public async Task SavingChangesAsync_WithNewTenantScopedEntity_SetsTenantId()
    {
        TenantId tenantId = TenantId.New();
        _tenantContext.IsResolved.Returns(true);
        _tenantContext.TenantId.Returns(tenantId);
        using TestDbContext dbContext = CreateDbContext();
        dbContext.TenantEntities.Add(new TestTenantEntity { Name = "Test" });

        await dbContext.SaveChangesAsync();

        TestTenantEntity saved = dbContext.TenantEntities.Single();
        saved.TenantId.Should().Be(tenantId);
    }

    [Fact]
    public void SavingChanges_WithUnresolvedTenantContext_DoesNotSetTenantId()
    {
        _tenantContext.IsResolved.Returns(false);
        using TestDbContext dbContext = CreateDbContext();
        dbContext.TenantEntities.Add(new TestTenantEntity { Name = "Test" });

        dbContext.SaveChanges();

        TestTenantEntity saved = dbContext.TenantEntities.Single();
        saved.TenantId.Should().Be(default(TenantId));
    }

    [Fact]
    public void SavingChanges_WithModifiedTenantScopedEntity_PreventsTenantIdChange()
    {
        TenantId originalTenantId = TenantId.New();
        _tenantContext.IsResolved.Returns(true);
        _tenantContext.TenantId.Returns(originalTenantId);
        using TestDbContext dbContext = CreateDbContext();
        dbContext.TenantEntities.Add(new TestTenantEntity { Name = "Test" });
        dbContext.SaveChanges();

        TestTenantEntity entity = dbContext.TenantEntities.Single();
        dbContext.Entry(entity).Property(nameof(ITenantScoped.TenantId)).CurrentValue = TenantId.New();
        entity.Name = "Updated";
        dbContext.SaveChanges();

        TestTenantEntity updated = dbContext.TenantEntities.Single();
        updated.TenantId.Should().Be(originalTenantId);
        updated.Name.Should().Be("Updated");
    }

    [Fact]
    public void SavingChanges_WithNonTenantScopedEntity_IsIgnored()
    {
        _tenantContext.IsResolved.Returns(true);
        _tenantContext.TenantId.Returns(TenantId.New());
        using TestDbContext dbContext = CreateDbContext();
        dbContext.NonTenantEntities.Add(new TestNonTenantEntity { Name = "Test" });

        Action act = () => dbContext.SaveChanges();

        act.Should().NotThrow();
        dbContext.NonTenantEntities.Should().ContainSingle()
            .Which.Name.Should().Be("Test");
    }

    [Fact]
    public void SavingChanges_WithMultipleNewEntities_SetsAllTenantIds()
    {
        TenantId tenantId = TenantId.New();
        _tenantContext.IsResolved.Returns(true);
        _tenantContext.TenantId.Returns(tenantId);
        using TestDbContext dbContext = CreateDbContext();
        dbContext.TenantEntities.Add(new TestTenantEntity { Name = "First" });
        dbContext.TenantEntities.Add(new TestTenantEntity { Name = "Second" });

        dbContext.SaveChanges();

        List<TestTenantEntity> saved = dbContext.TenantEntities.ToList();
        saved.Should().HaveCount(2);
        saved.Should().AllSatisfy(e => e.TenantId.Should().Be(tenantId));
    }

    private TestDbContext CreateDbContext()
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .AddInterceptors(new TenantSaveChangesInterceptor(_tenantContext))
            .Options;

        return new TestDbContext(options);
    }

    private sealed class TestTenantEntity : ITenantScoped
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public TenantId TenantId { get; init; }
    }

    private sealed class TestNonTenantEntity
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    private sealed class TestDbContext : DbContext
    {
        public TestDbContext(DbContextOptions<TestDbContext> options) : base(options) { }

        public DbSet<TestTenantEntity> TenantEntities => Set<TestTenantEntity>();
        public DbSet<TestNonTenantEntity> NonTenantEntities => Set<TestNonTenantEntity>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TestTenantEntity>(b =>
            {
                b.HasKey(e => e.Id);
                b.Property(e => e.TenantId)
                    .HasConversion(v => v.Value, v => TenantId.Create(v));
            });

            modelBuilder.Entity<TestNonTenantEntity>(b =>
            {
                b.HasKey(e => e.Id);
            });
        }
    }
}
