using Foundry.Shared.Kernel.Identity;
using Foundry.Shared.Kernel.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace Foundry.Shared.Kernel.Tests.MultiTenancy;

public class TenantQueryExtensionsTests
{
    private sealed class TestEntity : ITenantScoped
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public TenantId TenantId { get; init; }
    }

    private sealed class TestDbContext : DbContext
    {
        public DbSet<TestEntity> Entities => Set<TestEntity>();

        public TestDbContext(DbContextOptions<TestDbContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TestEntity>(e =>
            {
                e.HasKey(x => x.Id);
                e.Property(x => x.TenantId)
                    .HasConversion(v => v.Value, v => new TenantId(v));
                e.HasQueryFilter(x => x.TenantId == new TenantId(Guid.Empty));
            });
        }
    }

    private static TestDbContext CreateContext()
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TestDbContext(options);
    }

    [Fact]
    public void AllTenants_BypassesQueryFilter_ReturnsAllRecords()
    {
        using TestDbContext context = CreateContext();
        TenantId tenant1 = TenantId.New();
        TenantId tenant2 = TenantId.New();

        context.Entities.AddRange(
            new TestEntity { Id = 1, Name = "A", TenantId = tenant1 },
            new TestEntity { Id = 2, Name = "B", TenantId = tenant2 });
        context.SaveChanges();

        // Without AllTenants, query filter excludes both (filter matches Guid.Empty only)
        List<TestEntity> filtered = context.Entities.ToList();
        filtered.Should().BeEmpty();

        // With AllTenants, query filter is bypassed
        List<TestEntity> unfiltered = context.Entities.AllTenants().ToList();
        unfiltered.Should().HaveCount(2);
    }

    [Fact]
    public void AllTenants_OnEmptySet_ReturnsEmpty()
    {
        using TestDbContext context = CreateContext();

        List<TestEntity> result = context.Entities.AllTenants().ToList();

        result.Should().BeEmpty();
    }

    [Fact]
    public void AllTenants_CanChainWithWhereClause()
    {
        using TestDbContext context = CreateContext();
        TenantId tenant1 = TenantId.New();
        TenantId tenant2 = TenantId.New();

        context.Entities.AddRange(
            new TestEntity { Id = 1, Name = "Target", TenantId = tenant1 },
            new TestEntity { Id = 2, Name = "Other", TenantId = tenant2 });
        context.SaveChanges();

        List<TestEntity> result = context.Entities.AllTenants()
            .Where(e => e.Name == "Target")
            .ToList();

        result.Should().ContainSingle()
            .Which.TenantId.Should().Be(tenant1);
    }
}
