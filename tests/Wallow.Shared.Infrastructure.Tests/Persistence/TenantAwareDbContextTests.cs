using Wallow.Shared.Infrastructure.Core.Persistence;
using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.MultiTenancy;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Wallow.Shared.Infrastructure.Tests.Persistence;

public sealed class TenantAwareDbContextTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly TenantId _tenantA = TenantId.New();
    private readonly TenantId _tenantB = TenantId.New();

    public TenantAwareDbContextTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
    }

    private FakeDbContext CreateContext(TenantId tenantId)
    {
        ITenantContext tenantContext = Substitute.For<ITenantContext>();
        tenantContext.TenantId.Returns(tenantId);

        DbContextOptions<FakeDbContext> options = new DbContextOptionsBuilder<FakeDbContext>()
            .UseSqlite(_connection)
            .Options;

        FakeDbContext context = new(options, tenantContext);
        context.Database.EnsureCreated();
        return context;
    }

    [Fact]
    public void Query_WithTenantFilter_ReturnsOnlyCurrentTenantData()
    {
        using (FakeDbContext seedContext = CreateContext(_tenantA))
        {
            seedContext.TenantEntities.Add(new FakeTenantEntity { Id = Guid.NewGuid(), Name = "TenantA-Item", TenantId = _tenantA });
            seedContext.TenantEntities.Add(new FakeTenantEntity { Id = Guid.NewGuid(), Name = "TenantB-Item", TenantId = _tenantB });
            seedContext.SaveChanges();
        }

        using FakeDbContext contextA = CreateContext(_tenantA);

        List<FakeTenantEntity> results = contextA.TenantEntities.ToList();

        results.Should().HaveCount(1);
        results[0].Name.Should().Be("TenantA-Item");
        results[0].TenantId.Should().Be(_tenantA);
    }

    [Fact]
    public void Query_WithTenantFilter_DoesNotReturnCrossTenantData()
    {
        using (FakeDbContext seedContext = CreateContext(_tenantA))
        {
            seedContext.TenantEntities.Add(new FakeTenantEntity { Id = Guid.NewGuid(), Name = "TenantA-Only", TenantId = _tenantA });
            seedContext.TenantEntities.Add(new FakeTenantEntity { Id = Guid.NewGuid(), Name = "TenantB-Only", TenantId = _tenantB });
            seedContext.SaveChanges();
        }

        using FakeDbContext contextB = CreateContext(_tenantB);

        List<FakeTenantEntity> results = contextB.TenantEntities.ToList();

        results.Should().HaveCount(1);
        results[0].Name.Should().Be("TenantB-Only");
        results[0].TenantId.Should().Be(_tenantB);
    }

    [Fact]
    public void Query_NonTenantScopedEntity_ReturnsAllData()
    {
        using (FakeDbContext seedContext = CreateContext(_tenantA))
        {
            seedContext.NonTenantEntities.Add(new FakeNonTenantEntity { Id = Guid.NewGuid(), Name = "Global-1" });
            seedContext.NonTenantEntities.Add(new FakeNonTenantEntity { Id = Guid.NewGuid(), Name = "Global-2" });
            seedContext.SaveChanges();
        }

        using FakeDbContext contextB = CreateContext(_tenantB);

        List<FakeNonTenantEntity> results = contextB.NonTenantEntities.ToList();

        results.Should().HaveCount(2);
    }

    [Fact]
    public void Query_WithIgnoreQueryFilters_ReturnsCrossTenantData()
    {
        using (FakeDbContext seedContext = CreateContext(_tenantA))
        {
            seedContext.TenantEntities.Add(new FakeTenantEntity { Id = Guid.NewGuid(), Name = "A", TenantId = _tenantA });
            seedContext.TenantEntities.Add(new FakeTenantEntity { Id = Guid.NewGuid(), Name = "B", TenantId = _tenantB });
            seedContext.SaveChanges();
        }

        using FakeDbContext contextA = CreateContext(_tenantA);

        List<FakeTenantEntity> results = contextA.TenantEntities.IgnoreQueryFilters().ToList();

        results.Should().HaveCount(2);
    }

    public void Dispose()
    {
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }
}

public class FakeTenantEntity : ITenantScoped
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public TenantId TenantId { get; init; }
}

public class FakeNonTenantEntity
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
}

public class FakeDbContext : TenantAwareDbContext<FakeDbContext>
{
    public DbSet<FakeTenantEntity> TenantEntities => Set<FakeTenantEntity>();
    public DbSet<FakeNonTenantEntity> NonTenantEntities => Set<FakeNonTenantEntity>();

    public FakeDbContext(DbContextOptions<FakeDbContext> options, ITenantContext tenantContext)
        : base(options, tenantContext)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<FakeTenantEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TenantId)
                .HasConversion(t => t.Value, v => TenantId.Create(v));
        });

        modelBuilder.Entity<FakeNonTenantEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
        });

        ApplyTenantQueryFilters(modelBuilder);
    }
}
