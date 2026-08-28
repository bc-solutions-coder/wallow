using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Wallow.Shared.Contracts.Storage;
using Wallow.Shared.Kernel.Identity;
using Wallow.Storage.Domain.Entities;
using Wallow.Storage.Infrastructure.Jobs;
using Wallow.Storage.Infrastructure.Persistence;

namespace Wallow.Storage.Tests.Infrastructure;

public sealed class OrphanedObjectSweepJobTests : IDisposable
{
    private static readonly DateTimeOffset _now = new(2026, 8, 27, 12, 0, 0, TimeSpan.Zero);

    private readonly SqliteConnection _connection;
    private readonly StorageDbContext _context;
    private readonly IStorageProvider _storageProvider;
    private readonly OrphanedObjectSweepJob _job;
    private readonly TenantId _tenantId = TenantId.New();
    private readonly TenantId _otherTenantId = TenantId.New();

    public OrphanedObjectSweepJobTests()
    {
        // A shared open connection: with the ":memory:" connection STRING each EF operation
        // would open a fresh connection and see an empty database.
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        DbContextOptions<StorageDbContext> options = new DbContextOptionsBuilder<StorageDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new StorageDbContext(options);
        _context.SetTenant(_tenantId);
        _context.Database.EnsureCreated();

        _storageProvider = Substitute.For<IStorageProvider>();
        FakeTimeProvider timeProvider = new(_now);
        _job = new OrphanedObjectSweepJob(
            _context,
            _storageProvider,
            timeProvider,
            NullLogger<OrphanedObjectSweepJob>.Instance);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    private async Task SeedFileAsync(TenantId tenantId, string storageKey)
    {
        StorageBucket bucket = StorageBucket.Create(tenantId, "documents");
        StoredFile file = StoredFile.Create(
            tenantId, bucket.Id, "file.txt", "text/plain", 10, storageKey, Guid.NewGuid());
        _context.Buckets.Add(bucket);
        _context.Files.Add(file);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();
    }

    private void SetListedObjects(params StorageObjectInfo[] objects)
    {
        _storageProvider.ListAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(objects.ToAsyncEnumerable());
    }

    [Fact]
    public async Task ExecuteAsync_DeletesUnreferencedObjectOlderThanThreshold()
    {
        SetListedObjects(new StorageObjectInfo("tenant-a/documents/orphan.txt", _now.AddDays(-2)));

        int deleted = await _job.ExecuteAsync(CancellationToken.None);

        deleted.Should().Be(1);
        await _storageProvider.Received(1)
            .DeleteAsync("tenant-a/documents/orphan.txt", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_KeepsReferencedObject_EvenWhenTheRowBelongsToAnotherTenant()
    {
        // The row is written under a tenant the context is NOT set to, so keeping this
        // object proves the sweep reads Files across tenants (IgnoreQueryFilters) —
        // with the filter active the row would be invisible and the object deleted.
        const string key = "tenant-b/documents/kept.txt";
        await SeedFileAsync(_otherTenantId, key);
        SetListedObjects(new StorageObjectInfo(key, _now.AddDays(-2)));

        int deleted = await _job.ExecuteAsync(CancellationToken.None);

        deleted.Should().Be(0);
        await _storageProvider.DidNotReceiveWithAnyArgs().DeleteAsync(default!, default);
    }

    [Fact]
    public async Task ExecuteAsync_KeepsUnreferencedObjectYoungerThanThreshold()
    {
        SetListedObjects(new StorageObjectInfo("tenant-a/documents/in-flight.txt", _now.AddHours(-1)));

        int deleted = await _job.ExecuteAsync(CancellationToken.None);

        deleted.Should().Be(0);
        await _storageProvider.DidNotReceiveWithAnyArgs().DeleteAsync(default!, default);
    }

    [Fact]
    public async Task ExecuteAsync_ListsOnlyUnderTheTenantPrefix()
    {
        SetListedObjects();

        await _job.ExecuteAsync(CancellationToken.None);

        _storageProvider.Received(1).ListAsync("tenant-", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_SweepsOnlyOrphans_AndReturnsTheCount()
    {
        const string keptKey = "tenant-a/documents/kept.txt";
        await SeedFileAsync(_tenantId, keptKey);
        SetListedObjects(
            new StorageObjectInfo(keptKey, _now.AddDays(-2)),
            new StorageObjectInfo("tenant-a/documents/orphan-1.txt", _now.AddDays(-2)),
            new StorageObjectInfo("tenant-c/documents/orphan-2.txt", _now.AddDays(-3)));

        int deleted = await _job.ExecuteAsync(CancellationToken.None);

        deleted.Should().Be(2);
        await _storageProvider.Received(1)
            .DeleteAsync("tenant-a/documents/orphan-1.txt", Arg.Any<CancellationToken>());
        await _storageProvider.Received(1)
            .DeleteAsync("tenant-c/documents/orphan-2.txt", Arg.Any<CancellationToken>());
        await _storageProvider.DidNotReceive().DeleteAsync(keptKey, Arg.Any<CancellationToken>());
    }
}
