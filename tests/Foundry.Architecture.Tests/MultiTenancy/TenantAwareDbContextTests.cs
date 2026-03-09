using Foundry.Shared.Infrastructure.Core.Persistence;
using Foundry.Shared.Kernel.Identity;
using Foundry.Shared.Kernel.MultiTenancy;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using NSubstitute;

namespace Foundry.Architecture.Tests.MultiTenancy;

public sealed class TenantAwareDbContextTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ITenantContext _tenantContext;

    public TenantAwareDbContextTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _tenantContext = Substitute.For<ITenantContext>();
        _tenantContext.TenantId.Returns(TenantId.New());
    }

    [Fact]
    public void ApplyTenantQueryFilters_ShouldApplyFilterToTenantScopedEntity()
    {
        using TestDbContext context = CreateContext();

        IEntityType? entityType = context.Model.FindEntityType(typeof(TenantScopedEntity));

        entityType.Should().NotBeNull();
        entityType.GetDeclaredQueryFilters().Should().NotBeEmpty(
            "ITenantScoped entities should have a tenant query filter applied");
    }

    [Fact]
    public void ApplyTenantQueryFilters_ShouldNotApplyFilterToNonTenantEntity()
    {
        using TestDbContext context = CreateContext();

        IEntityType? entityType = context.Model.FindEntityType(typeof(NonTenantEntity));

        entityType.Should().NotBeNull();
        entityType.GetDeclaredQueryFilters().Should().BeEmpty(
            "non-ITenantScoped entities should not have a tenant query filter");
    }

    private TestDbContext CreateContext()
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseSqlite(_connection)
            .Options;

        TestDbContext context = new(options, _tenantContext);
        context.Database.EnsureCreated();
        return context;
    }

    public void Dispose()
    {
        _connection.Dispose();
    }

    private sealed class TestDbContext : TenantAwareDbContext<TestDbContext>
    {
        // ReSharper disable once UnusedMember.Local
        public DbSet<TenantScopedEntity> TenantScopedEntities => Set<TenantScopedEntity>();
        // ReSharper disable once UnusedMember.Local
        public DbSet<NonTenantEntity> NonTenantEntities => Set<NonTenantEntity>();

        public TestDbContext(DbContextOptions<TestDbContext> options, ITenantContext tenantContext)
            : base(options, tenantContext)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<TenantScopedEntity>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.TenantId)
                    .HasConversion(t => t.Value, v => TenantId.Create(v));
            });

            modelBuilder.Entity<NonTenantEntity>(entity =>
            {
                entity.HasKey(e => e.Id);
            });

            ApplyTenantQueryFilters(modelBuilder);
        }
    }

    private sealed class TenantScopedEntity : ITenantScoped
    {
        public Guid Id { get; set; }
        public TenantId TenantId { get; init; }
    }

    private sealed class NonTenantEntity
    {
        public Guid Id { get; set; }
    }
}
