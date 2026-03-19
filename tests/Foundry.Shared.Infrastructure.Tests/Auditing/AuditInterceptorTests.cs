using System.Text.Json;
using Foundry.Shared.Infrastructure.Core.Auditing;
using Foundry.Shared.Kernel.Auditing;
using Foundry.Shared.Kernel.MultiTenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Testcontainers.PostgreSql;

namespace Foundry.Shared.Infrastructure.Tests.Auditing;

public class AuditTestEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Value { get; set; }
}

public class CompositeKeyEntity
{
    public Guid PartA { get; set; }
    public Guid PartB { get; set; }
    public string Label { get; set; } = string.Empty;
}

public class AuditIgnoreEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;

    [AuditIgnore]
    public string Secret { get; set; } = string.Empty;
}

public class AuditTestDbContext : DbContext
{
    public DbSet<AuditTestEntity> AuditTestEntities => Set<AuditTestEntity>();
    public DbSet<CompositeKeyEntity> CompositeKeyEntities => Set<CompositeKeyEntity>();
    public DbSet<AuditIgnoreEntity> AuditIgnoreEntities => Set<AuditIgnoreEntity>();

    public AuditTestDbContext(DbContextOptions<AuditTestDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("audit_test");
        modelBuilder.Entity<AuditTestEntity>(e =>
        {
            e.ToTable("audit_test_entities");
            e.HasKey(x => x.Id);
        });
        modelBuilder.Entity<CompositeKeyEntity>(e =>
        {
            e.ToTable("composite_key_entities");
            e.HasKey(x => new { x.PartA, x.PartB });
        });
        modelBuilder.Entity<AuditIgnoreEntity>(e =>
        {
            e.ToTable("audit_ignore_entities");
            e.HasKey(x => x.Id);
        });
    }
}

public class AuditInterceptorTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18-alpine")
        .Build();

    private ServiceProvider _serviceProvider = null!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        ServiceCollection services = new ServiceCollection();

        IHttpContextAccessor httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        services.AddSingleton(httpContextAccessor);

        ITenantContext tenantContext = Substitute.For<ITenantContext>();
        tenantContext.IsResolved.Returns(false);
        services.AddSingleton(tenantContext);

        services.AddLogging();
        services.AddSingleton<AuditInterceptor>();

        services.AddDbContext<AuditDbContext>(options =>
            options.UseNpgsql(_postgres.GetConnectionString(), npgsql =>
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "audit")));

        services.AddDbContext<AuditTestDbContext>((sp, options) =>
        {
            AuditInterceptor interceptor = sp.GetRequiredService<AuditInterceptor>();
            options.UseNpgsql(_postgres.GetConnectionString())
                .AddInterceptors(interceptor);
        });

        _serviceProvider = services.BuildServiceProvider();

        using IServiceScope scope = _serviceProvider.CreateScope();

        // Create schemas and tables via raw SQL to avoid EnsureCreatedAsync pitfall:
        // EnsureCreatedAsync is all-or-nothing — if any tables exist, the second call is a no-op.
        AuditDbContext auditDb = scope.ServiceProvider.GetRequiredService<AuditDbContext>();
        await auditDb.Database.EnsureCreatedAsync();

        AuditTestDbContext testDb = scope.ServiceProvider.GetRequiredService<AuditTestDbContext>();
        RelationalDatabaseCreator creator = (RelationalDatabaseCreator)
            ((IInfrastructure<IServiceProvider>)testDb.Database).Instance
                .GetRequiredService<IDatabaseCreator>();
        await creator.CreateTablesAsync();
    }

    public async Task DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    [Fact]
    public async Task Insert_CreatesAuditEntry_WithInsertAction()
    {
        using IServiceScope scope = _serviceProvider.CreateScope();
        AuditTestDbContext testDb = scope.ServiceProvider.GetRequiredService<AuditTestDbContext>();
        AuditDbContext auditDb = scope.ServiceProvider.GetRequiredService<AuditDbContext>();

        AuditTestEntity entity = new AuditTestEntity { Id = Guid.NewGuid(), Name = "Test", Value = 42 };
        testDb.AuditTestEntities.Add(entity);
        await testDb.SaveChangesAsync();

        List<AuditEntry> auditEntries = await auditDb.AuditEntries
            .Where(e => e.EntityId == entity.Id.ToString())
            .ToListAsync();

        auditEntries.Should().ContainSingle();
        AuditEntry audit = auditEntries[0];
        audit.Action.Should().Be("Insert");
        audit.EntityType.Should().Be("AuditTestEntity");
        audit.OldValues.Should().BeNull();
        audit.NewValues.Should().NotBeNull();

        Dictionary<string, JsonElement>? newValues = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(audit.NewValues);
        newValues.Should().ContainKey("Name");
        newValues["Name"].GetString().Should().Be("Test");
    }

    [Fact]
    public async Task Update_CreatesAuditEntry_WithOldAndNewValues()
    {
        using IServiceScope scope = _serviceProvider.CreateScope();
        AuditTestDbContext testDb = scope.ServiceProvider.GetRequiredService<AuditTestDbContext>();
        AuditDbContext auditDb = scope.ServiceProvider.GetRequiredService<AuditDbContext>();

        AuditTestEntity entity = new AuditTestEntity { Id = Guid.NewGuid(), Name = "Original", Value = 1 };
        testDb.AuditTestEntities.Add(entity);
        await testDb.SaveChangesAsync();

        entity.Name = "Updated";
        entity.Value = 2;
        await testDb.SaveChangesAsync();

        List<AuditEntry> auditEntries = await auditDb.AuditEntries
            .Where(e => e.EntityId == entity.Id.ToString() && e.Action == "Update")
            .ToListAsync();

        auditEntries.Should().ContainSingle();
        AuditEntry audit = auditEntries[0];
        audit.OldValues.Should().NotBeNull();
        audit.NewValues.Should().NotBeNull();

        Dictionary<string, JsonElement>? oldValues = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(audit.OldValues!);
        Dictionary<string, JsonElement>? newValues = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(audit.NewValues!);
        oldValues!["Name"].GetString().Should().Be("Original");
        newValues!["Name"].GetString().Should().Be("Updated");
    }

    [Fact]
    public async Task Delete_CreatesAuditEntry_WithOldValues()
    {
        using IServiceScope scope = _serviceProvider.CreateScope();
        AuditTestDbContext testDb = scope.ServiceProvider.GetRequiredService<AuditTestDbContext>();
        AuditDbContext auditDb = scope.ServiceProvider.GetRequiredService<AuditDbContext>();

        AuditTestEntity entity = new AuditTestEntity { Id = Guid.NewGuid(), Name = "ToDelete", Value = 99 };
        testDb.AuditTestEntities.Add(entity);
        await testDb.SaveChangesAsync();

        testDb.AuditTestEntities.Remove(entity);
        await testDb.SaveChangesAsync();

        List<AuditEntry> auditEntries = await auditDb.AuditEntries
            .Where(e => e.EntityId == entity.Id.ToString() && e.Action == "Delete")
            .ToListAsync();

        auditEntries.Should().ContainSingle();
        AuditEntry audit = auditEntries[0];
        audit.OldValues.Should().NotBeNull();
        audit.NewValues.Should().BeNull();

        Dictionary<string, JsonElement>? oldValues = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(audit.OldValues!);
        oldValues!["Name"].GetString().Should().Be("ToDelete");
        oldValues["Value"].GetInt32().Should().Be(99);
    }

    [Fact]
    public async Task Insert_CompositeKey_SerializesAsCommaSeparatedString()
    {
        using IServiceScope scope = _serviceProvider.CreateScope();
        AuditTestDbContext testDb = scope.ServiceProvider.GetRequiredService<AuditTestDbContext>();
        AuditDbContext auditDb = scope.ServiceProvider.GetRequiredService<AuditDbContext>();

        Guid partA = Guid.NewGuid();
        Guid partB = Guid.NewGuid();
        testDb.CompositeKeyEntities.Add(new CompositeKeyEntity { PartA = partA, PartB = partB, Label = "Composite" });
        await testDb.SaveChangesAsync();

        List<AuditEntry> auditEntries = await auditDb.AuditEntries
            .Where(e => e.EntityType == "CompositeKeyEntity")
            .ToListAsync();

        auditEntries.Should().ContainSingle();
        auditEntries[0].EntityId.Should().Be($"{partA},{partB}");
    }

    [Fact]
    public async Task Delete_CapturesOldValuesOnly_NewValuesIsNull()
    {
        using IServiceScope scope = _serviceProvider.CreateScope();
        AuditTestDbContext testDb = scope.ServiceProvider.GetRequiredService<AuditTestDbContext>();
        AuditDbContext auditDb = scope.ServiceProvider.GetRequiredService<AuditDbContext>();

        AuditTestEntity entity = new() { Id = Guid.NewGuid(), Name = "DeleteMe", Value = 77 };
        testDb.AuditTestEntities.Add(entity);
        await testDb.SaveChangesAsync();

        testDb.AuditTestEntities.Remove(entity);
        await testDb.SaveChangesAsync();

        AuditEntry audit = (await auditDb.AuditEntries
            .Where(e => e.EntityId == entity.Id.ToString() && e.Action == "Delete")
            .ToListAsync()).Single();

        audit.NewValues.Should().BeNull();
        audit.OldValues.Should().NotBeNull();

        Dictionary<string, JsonElement> oldValues = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(audit.OldValues!)!;
        oldValues.Should().ContainKeys("Id", "Name", "Value");
        oldValues["Name"].GetString().Should().Be("DeleteMe");
        oldValues["Value"].GetInt32().Should().Be(77);
    }

    [Fact]
    public async Task SavingChangesAsync_WhenContextIsAuditDbContext_SkipsAuditCapture()
    {
        using IServiceScope scope = _serviceProvider.CreateScope();
        AuditDbContext auditDb = scope.ServiceProvider.GetRequiredService<AuditDbContext>();

        int countBefore = await auditDb.AuditEntries.CountAsync();

        // Directly add an AuditEntry to AuditDbContext — the interceptor should skip it
        auditDb.AuditEntries.Add(new AuditEntry
        {
            Id = Guid.NewGuid(),
            EntityType = "ManualEntry",
            EntityId = "manual-1",
            Action = "Insert",
            Timestamp = DateTimeOffset.UtcNow
        });
        await auditDb.SaveChangesAsync();

        int countAfter = await auditDb.AuditEntries.CountAsync();

        // Only the manually added entry should exist — no recursive audit-of-audit entry
        (countAfter - countBefore).Should().Be(1);
        (await auditDb.AuditEntries.Where(e => e.EntityId == "manual-1").CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task SavingChangesAsync_WhenServicesUnavailable_ContinuesWithoutThrowing()
    {
        // Build a minimal service provider WITHOUT IHttpContextAccessor or ITenantContext
        ServiceCollection services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<AuditInterceptor>();

        services.AddDbContext<AuditDbContext>(options =>
            options.UseNpgsql(_postgres.GetConnectionString(), npgsql =>
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "audit")));

        services.AddDbContext<AuditTestDbContext>((sp, options) =>
        {
            AuditInterceptor interceptor = sp.GetRequiredService<AuditInterceptor>();
            options.UseNpgsql(_postgres.GetConnectionString())
                .AddInterceptors(interceptor);
        });

        await using ServiceProvider sp = services.BuildServiceProvider();
        using IServiceScope scope = sp.CreateScope();

        AuditTestDbContext testDb = scope.ServiceProvider.GetRequiredService<AuditTestDbContext>();
        AuditDbContext auditDb = scope.ServiceProvider.GetRequiredService<AuditDbContext>();

        AuditTestEntity entity = new() { Id = Guid.NewGuid(), Name = "NoServices", Value = 55 };
        testDb.AuditTestEntities.Add(entity);

        // Should not throw even though IHttpContextAccessor and ITenantContext are missing
        Func<Task> act = async () => await testDb.SaveChangesAsync();
        await act.Should().NotThrowAsync();

        // Audit entry should still be created (with null userId and tenantId)
        AuditEntry audit = (await auditDb.AuditEntries
            .Where(e => e.EntityId == entity.Id.ToString())
            .ToListAsync()).Single();

        audit.UserId.Should().BeNull();
        audit.TenantId.Should().BeNull();
        audit.Action.Should().Be("Insert");
    }

    [Fact]
    public async Task SavingChangesAsync_WhenContextIsNull_ReturnsWithoutCapture()
    {
        using IServiceScope scope = _serviceProvider.CreateScope();
        AuditInterceptor interceptor = scope.ServiceProvider.GetRequiredService<AuditInterceptor>();
        AuditDbContext auditDb = scope.ServiceProvider.GetRequiredService<AuditDbContext>();

        int countBefore = await auditDb.AuditEntries.CountAsync();

        DbContextEventData eventData = Substitute.For<DbContextEventData>(
            null, null, null);
        eventData.Context.Returns((DbContext?)null);

        await interceptor.SavingChangesAsync(eventData, default);

        int countAfter = await auditDb.AuditEntries.CountAsync();
        countAfter.Should().Be(countBefore);
    }

    [Fact]
    public async Task SavingChangesAsync_WhenScopeCreationFails_ContinuesWithoutThrowing()
    {
        // Build a real service provider that has AuditDbContext for SaveAuditEntriesAsync
        ServiceCollection services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<AuditDbContext>(options =>
            options.UseNpgsql(_postgres.GetConnectionString(), npgsql =>
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "audit")));
        services.AddDbContext<AuditTestDbContext>(options =>
            options.UseNpgsql(_postgres.GetConnectionString()));
        await using ServiceProvider realProvider = services.BuildServiceProvider();

        // Wrap the real provider: first CreateScope call (CaptureChanges) throws,
        // subsequent calls (SaveAuditEntriesAsync via CreateAsyncScope) delegate to the real provider
        ScopeFailOnceServiceProvider failOnceProvider = new ScopeFailOnceServiceProvider(realProvider);

        AuditInterceptor interceptor = new AuditInterceptor(
            failOnceProvider,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<AuditInterceptor>.Instance);

        using IServiceScope testScope = realProvider.CreateScope();
        AuditTestDbContext testDb = testScope.ServiceProvider.GetRequiredService<AuditTestDbContext>();

        Guid entityId = Guid.NewGuid();
        testDb.AuditTestEntities.Add(new AuditTestEntity { Id = entityId, Name = "CatchBlock", Value = 1 });

        DbContextEventData eventData = Substitute.For<DbContextEventData>(
            null, null, null);
        eventData.Context.Returns(testDb);

        // Should not throw — CaptureChanges catch block swallows the scope failure,
        // entries are still captured (with null userId/tenantId), and SaveAuditEntriesAsync succeeds
        Func<Task> act = async () => await interceptor.SavingChangesAsync(eventData, default);
        await act.Should().NotThrowAsync();

        // Verify audit entry was created with null user/tenant context
        AuditDbContext auditDb = testScope.ServiceProvider.GetRequiredService<AuditDbContext>();
        AuditEntry audit = (await auditDb.AuditEntries
            .Where(e => e.EntityId == entityId.ToString())
            .ToListAsync()).Single();

        audit.UserId.Should().BeNull();
        audit.TenantId.Should().BeNull();
        audit.Action.Should().Be("Insert");
    }

    /// <summary>
    /// Wraps a real IServiceProvider but makes the first CreateScope call fail,
    /// simulating a transient scope creation failure in CaptureChanges.
    /// </summary>
    private sealed class ScopeFailOnceServiceProvider(IServiceProvider inner) : IServiceProvider
    {
        private int _scopeRequestCount;

        public object? GetService(Type serviceType)
        {
            if (serviceType == typeof(IServiceScopeFactory))
            {
                if (Interlocked.Increment(ref _scopeRequestCount) == 1)
                {
                    throw new InvalidOperationException("Simulated scope creation failure");
                }
            }

            return inner.GetService(serviceType);
        }
    }

    [Fact]
    public async Task Insert_WithAuditIgnoreProperty_ExcludesIgnoredPropertyFromValues()
    {
        using IServiceScope scope = _serviceProvider.CreateScope();
        AuditTestDbContext testDb = scope.ServiceProvider.GetRequiredService<AuditTestDbContext>();
        AuditDbContext auditDb = scope.ServiceProvider.GetRequiredService<AuditDbContext>();

        AuditIgnoreEntity entity = new()
        {
            Id = Guid.NewGuid(),
            Name = "Visible",
            Secret = "ShouldBeExcluded"
        };
        testDb.AuditIgnoreEntities.Add(entity);
        await testDb.SaveChangesAsync();

        AuditEntry audit = (await auditDb.AuditEntries
            .Where(e => e.EntityId == entity.Id.ToString())
            .ToListAsync()).Single();

        Dictionary<string, JsonElement> newValues = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(audit.NewValues!)!;
        newValues.Should().ContainKey("Name");
        newValues.Should().ContainKey("Id");
        newValues.Should().NotContainKey("Secret");
    }
}
