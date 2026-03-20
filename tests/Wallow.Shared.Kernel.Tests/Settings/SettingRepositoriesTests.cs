using Wallow.Shared.Infrastructure.Core.Persistence;
using Wallow.Shared.Infrastructure.Settings;
using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace Wallow.Shared.Kernel.Tests.Settings;

public class SettingRepositoriesTests
{
    private readonly TenantId _tenantId = TenantId.New();

    private TestDbContext CreateContext()
    {
        ITenantContext tenantContext = Substitute.For<ITenantContext>();
        tenantContext.IsResolved.Returns(true);
        tenantContext.TenantId.Returns(_tenantId);

        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new TestDbContext(options, tenantContext);
    }

    // ── TenantSettingRepository ──────────────────────────────────────────

    [Fact]
    public async Task TenantSettingRepository_GetAsync_WhenNotExists_ReturnsNull()
    {
        await using TestDbContext context = CreateContext();
        TenantSettingRepository<TestDbContext> repo = new(context);

        TenantSettingEntity? result = await repo.GetAsync(_tenantId, "billing", "feature.enabled");

        result.Should().BeNull();
    }

    [Fact]
    public async Task TenantSettingRepository_GetAllAsync_WhenEmpty_ReturnsEmptyList()
    {
        await using TestDbContext context = CreateContext();
        TenantSettingRepository<TestDbContext> repo = new(context);

        IReadOnlyList<TenantSettingEntity> result = await repo.GetAllAsync(_tenantId, "billing");

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task TenantSettingRepository_UpsertAsync_InsertsNewEntity()
    {
        await using TestDbContext context = CreateContext();
        TenantSettingRepository<TestDbContext> repo = new(context);
        TenantSettingEntity entity = new(_tenantId, "billing", "feature.enabled", "true");

        await repo.UpsertAsync(entity);

        IReadOnlyList<TenantSettingEntity> all = await repo.GetAllAsync(_tenantId, "billing");
        all.Should().HaveCount(1);
        all[0].Value.Should().Be("true");
    }

    [Fact]
    public async Task TenantSettingRepository_UpsertAsync_UpdatesExistingEntity()
    {
        await using TestDbContext context = CreateContext();
        TenantSettingRepository<TestDbContext> repo = new(context);
        TenantSettingEntity entity = new(_tenantId, "billing", "feature.enabled", "false");
        await repo.UpsertAsync(entity);

        TenantSettingEntity update = new(_tenantId, "billing", "feature.enabled", "true");
        await repo.UpsertAsync(update);

        IReadOnlyList<TenantSettingEntity> all = await repo.GetAllAsync(_tenantId, "billing");
        all.Should().HaveCount(1);
        all[0].Value.Should().Be("true");
    }

    [Fact]
    public async Task TenantSettingRepository_GetAsync_ReturnsCorrectEntity()
    {
        await using TestDbContext context = CreateContext();
        TenantSettingRepository<TestDbContext> repo = new(context);
        TenantSettingEntity entity = new(_tenantId, "billing", "max.users", "100");
        await repo.UpsertAsync(entity);

        TenantSettingEntity? result = await repo.GetAsync(_tenantId, "billing", "max.users");

        result.Should().NotBeNull();
        result!.Value.Should().Be("100");
    }

    [Fact]
    public async Task TenantSettingRepository_DeleteAsync_RemovesEntity()
    {
        await using TestDbContext context = CreateContext();
        TenantSettingRepository<TestDbContext> repo = new(context);
        TenantSettingEntity entity = new(_tenantId, "billing", "feature.enabled", "true");
        await repo.UpsertAsync(entity);

        await repo.DeleteAsync(_tenantId, "billing", "feature.enabled");

        IReadOnlyList<TenantSettingEntity> all = await repo.GetAllAsync(_tenantId, "billing");
        all.Should().BeEmpty();
    }

    [Fact]
    public async Task TenantSettingRepository_DeleteAsync_WhenNotExists_DoesNotThrow()
    {
        await using TestDbContext context = CreateContext();
        TenantSettingRepository<TestDbContext> repo = new(context);

        Func<Task> act = () => repo.DeleteAsync(_tenantId, "billing", "nonexistent.key");

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task TenantSettingRepository_GetAllAsync_ReturnsOnlyModuleSettings()
    {
        await using TestDbContext context = CreateContext();
        TenantSettingRepository<TestDbContext> repo = new(context);
        await repo.UpsertAsync(new TenantSettingEntity(_tenantId, "billing", "key1", "val1"));
        await repo.UpsertAsync(new TenantSettingEntity(_tenantId, "identity", "key2", "val2"));

        IReadOnlyList<TenantSettingEntity> billingSettings = await repo.GetAllAsync(_tenantId, "billing");

        billingSettings.Should().HaveCount(1);
        billingSettings[0].SettingKey.Should().Be("key1");
    }

    // ── UserSettingRepository ────────────────────────────────────────────

    [Fact]
    public async Task UserSettingRepository_GetAsync_WhenNotExists_ReturnsNull()
    {
        await using TestDbContext context = CreateContext();
        UserSettingRepository<TestDbContext> repo = new(context);
        string userId = Guid.NewGuid().ToString("D");

        UserSettingEntity? result = await repo.GetAsync(_tenantId, userId, "billing", "theme");

        result.Should().BeNull();
    }

    [Fact]
    public async Task UserSettingRepository_GetAllAsync_WhenEmpty_ReturnsEmptyList()
    {
        await using TestDbContext context = CreateContext();
        UserSettingRepository<TestDbContext> repo = new(context);
        string userId = Guid.NewGuid().ToString("D");

        IReadOnlyList<UserSettingEntity> result = await repo.GetAllAsync(_tenantId, userId, "billing");

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task UserSettingRepository_UpsertAsync_InsertsNewEntity()
    {
        await using TestDbContext context = CreateContext();
        UserSettingRepository<TestDbContext> repo = new(context);
        string userId = Guid.NewGuid().ToString("D");
        UserSettingEntity entity = new(_tenantId, userId, "identity", "theme", "dark");

        await repo.UpsertAsync(entity);

        IReadOnlyList<UserSettingEntity> all = await repo.GetAllAsync(_tenantId, userId, "identity");
        all.Should().HaveCount(1);
        all[0].Value.Should().Be("dark");
    }

    [Fact]
    public async Task UserSettingRepository_UpsertAsync_UpdatesExistingEntity()
    {
        await using TestDbContext context = CreateContext();
        UserSettingRepository<TestDbContext> repo = new(context);
        string userId = Guid.NewGuid().ToString("D");
        await repo.UpsertAsync(new UserSettingEntity(_tenantId, userId, "identity", "theme", "light"));

        await repo.UpsertAsync(new UserSettingEntity(_tenantId, userId, "identity", "theme", "dark"));

        IReadOnlyList<UserSettingEntity> all = await repo.GetAllAsync(_tenantId, userId, "identity");
        all.Should().HaveCount(1);
        all[0].Value.Should().Be("dark");
    }

    [Fact]
    public async Task UserSettingRepository_DeleteAsync_RemovesEntity()
    {
        await using TestDbContext context = CreateContext();
        UserSettingRepository<TestDbContext> repo = new(context);
        string userId = Guid.NewGuid().ToString("D");
        await repo.UpsertAsync(new UserSettingEntity(_tenantId, userId, "identity", "theme", "dark"));

        await repo.DeleteAsync(_tenantId, userId, "identity", "theme");

        IReadOnlyList<UserSettingEntity> all = await repo.GetAllAsync(_tenantId, userId, "identity");
        all.Should().BeEmpty();
    }

    [Fact]
    public async Task UserSettingRepository_DeleteAsync_WhenNotExists_DoesNotThrow()
    {
        await using TestDbContext context = CreateContext();
        UserSettingRepository<TestDbContext> repo = new(context);
        string userId = Guid.NewGuid().ToString("D");

        Func<Task> act = () => repo.DeleteAsync(_tenantId, userId, "identity", "nonexistent");

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task UserSettingRepository_GetAsync_ReturnsCorrectEntity()
    {
        await using TestDbContext context = CreateContext();
        UserSettingRepository<TestDbContext> repo = new(context);
        string userId = Guid.NewGuid().ToString("D");
        await repo.UpsertAsync(new UserSettingEntity(_tenantId, userId, "billing", "currency", "EUR"));

        UserSettingEntity? result = await repo.GetAsync(_tenantId, userId, "billing", "currency");

        result.Should().NotBeNull();
        result!.Value.Should().Be("EUR");
    }

    [Fact]
    public async Task UserSettingRepository_GetAllAsync_OnlyReturnsCorrectUserSettings()
    {
        await using TestDbContext context = CreateContext();
        UserSettingRepository<TestDbContext> repo = new(context);
        string userId1 = Guid.NewGuid().ToString("D");
        string userId2 = Guid.NewGuid().ToString("D");
        await repo.UpsertAsync(new UserSettingEntity(_tenantId, userId1, "billing", "theme", "dark"));
        await repo.UpsertAsync(new UserSettingEntity(_tenantId, userId2, "billing", "theme", "light"));

        IReadOnlyList<UserSettingEntity> user1Settings = await repo.GetAllAsync(_tenantId, userId1, "billing");

        user1Settings.Should().HaveCount(1);
        user1Settings[0].Value.Should().Be("dark");
    }

    // ── Test support ─────────────────────────────────────────────────────

    private sealed class TestDbContext(DbContextOptions<TestDbContext> options, ITenantContext tenantContext)
        : TenantAwareDbContext<TestDbContext>(options, tenantContext)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<TenantSettingEntity>(b =>
            {
                b.HasKey(e => e.Id);
                b.Property(e => e.Id)
                    .HasConversion(v => v.Value, v => TenantSettingId.Create(v));
                b.Property(e => e.TenantId)
                    .HasConversion(v => v.Value, v => TenantId.Create(v));
                b.Ignore(e => e.CreatedAt);
                b.Ignore(e => e.UpdatedAt);
                b.Ignore(e => e.CreatedBy);
                b.Ignore(e => e.UpdatedBy);
            });

            modelBuilder.Entity<UserSettingEntity>(b =>
            {
                b.HasKey(e => e.Id);
                b.Property(e => e.Id)
                    .HasConversion(v => v.Value, v => UserSettingId.Create(v));
                b.Property(e => e.TenantId)
                    .HasConversion(v => v.Value, v => TenantId.Create(v));
                b.Ignore(e => e.CreatedAt);
                b.Ignore(e => e.UpdatedAt);
                b.Ignore(e => e.CreatedBy);
                b.Ignore(e => e.UpdatedBy);
            });
        }
    }
}
