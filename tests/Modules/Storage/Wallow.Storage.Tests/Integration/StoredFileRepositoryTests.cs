using Bogus;
using Wallow.Shared.Kernel.Identity;
using Wallow.Storage.Domain.Entities;
using Wallow.Storage.Infrastructure.Persistence;
using Wallow.Storage.Infrastructure.Persistence.Repositories;
using Wallow.Tests.Common.Bases;
using Wallow.Tests.Common.Fixtures;

namespace Wallow.Storage.Tests.Integration;

[CollectionDefinition("PostgresDatabase")]
public class PostgresDatabaseCollection : ICollectionFixture<PostgresContainerFixture>;

[Collection("PostgresDatabase")]
[Trait("Category", "Integration")]
public sealed class StoredFileRepositoryTests(PostgresContainerFixture fixture) : DbContextIntegrationTestBase<StorageDbContext>(fixture)
{
    private StoredFileRepository _repository = null!;
    private readonly Faker _faker = new();

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _repository = new StoredFileRepository(DbContext);
    }

    [Fact]
    public async Task Add_PersistsStoredFile()
    {
        StorageBucket bucket = StorageBucket.Create(TenantId.New(), $"test-bucket-{Guid.NewGuid()}");
        DbContext.Buckets.Add(bucket);
        await DbContext.SaveChangesAsync();

        StoredFile file = StoredFile.Create(
            TestTenantId,
            bucket.Id,
            _faker.System.FileName(),
            "application/pdf",
            1024 * 100,
            $"s3://bucket/{Guid.NewGuid()}/file.pdf",
            Guid.NewGuid(),
            "documents/invoices");

        _repository.Add(file);
        await _repository.SaveChangesAsync();

        StoredFile? retrieved = await _repository.GetByIdAsync(file.Id);

        retrieved.Should().NotBeNull();
        retrieved.FileName.Should().Be(file.FileName);
        retrieved.ContentType.Should().Be("application/pdf");
        retrieved.SizeBytes.Should().Be(1024 * 100);
        retrieved.Path.Should().Be("documents/invoices");
    }

    [Fact]
    public async Task GetByBucketIdAsync_FiltersByBucket()
    {
        string suffix = Guid.NewGuid().ToString()[..8];
        StorageBucket bucket1 = StorageBucket.Create(TenantId.New(), $"bucket1-{suffix}");
        StorageBucket bucket2 = StorageBucket.Create(TenantId.New(), $"bucket2-{suffix}");
        DbContext.Buckets.Add(bucket1);
        DbContext.Buckets.Add(bucket2);
        await DbContext.SaveChangesAsync();

        StoredFile file1 = StoredFile.Create(TestTenantId, bucket1.Id, "file1.txt", "text/plain", 100, Guid.NewGuid().ToString(), Guid.NewGuid());
        StoredFile file2 = StoredFile.Create(TestTenantId, bucket1.Id, "file2.txt", "text/plain", 200, Guid.NewGuid().ToString(), Guid.NewGuid());
        StoredFile file3 = StoredFile.Create(TestTenantId, bucket2.Id, "file3.txt", "text/plain", 300, Guid.NewGuid().ToString(), Guid.NewGuid());

        _repository.Add(file1);
        _repository.Add(file2);
        _repository.Add(file3);
        await _repository.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        IReadOnlyList<StoredFile> results = await _repository.GetByBucketIdAsync(bucket1.Id);

        results.Should().HaveCount(2);
        results.Should().AllSatisfy(f => f.BucketId.Should().Be(bucket1.Id));
    }

    [Fact]
    public async Task GetByBucketIdAsync_FiltersByPathPrefix()
    {
        StorageBucket bucket = StorageBucket.Create(TenantId.New(), $"test-bucket-{Guid.NewGuid()}");
        DbContext.Buckets.Add(bucket);
        await DbContext.SaveChangesAsync();

        StoredFile file1 = StoredFile.Create(TestTenantId, bucket.Id, "file1.txt", "text/plain", 100, Guid.NewGuid().ToString(), Guid.NewGuid(), "documents/invoices");
        StoredFile file2 = StoredFile.Create(TestTenantId, bucket.Id, "file2.txt", "text/plain", 200, Guid.NewGuid().ToString(), Guid.NewGuid(), "documents/receipts");
        StoredFile file3 = StoredFile.Create(TestTenantId, bucket.Id, "file3.txt", "text/plain", 300, Guid.NewGuid().ToString(), Guid.NewGuid(), "images/logos");

        _repository.Add(file1);
        _repository.Add(file2);
        _repository.Add(file3);
        await _repository.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        IReadOnlyList<StoredFile> results = await _repository.GetByBucketIdAsync(bucket.Id, "documents/");

        results.Should().HaveCount(2);
        results.Should().AllSatisfy(f => f.Path!.StartsWith("documents/", StringComparison.Ordinal).Should().BeTrue());
    }


    [Fact]
    public async Task Remove_DeletesFile()
    {
        StorageBucket bucket = StorageBucket.Create(TenantId.New(), $"test-bucket-{Guid.NewGuid()}");
        DbContext.Buckets.Add(bucket);
        await DbContext.SaveChangesAsync();

        StoredFile file = StoredFile.Create(TestTenantId, bucket.Id, "delete-me.txt", "text/plain", 100, Guid.NewGuid().ToString(), Guid.NewGuid());
        _repository.Add(file);
        await _repository.SaveChangesAsync();

        _repository.Remove(file);
        await _repository.SaveChangesAsync();

        StoredFile? retrieved = await _repository.GetByIdAsync(file.Id);
        retrieved.Should().BeNull();
    }

    [Fact]
    public async Task RespectsTenantIsolation()
    {
        string suffix = Guid.NewGuid().ToString()[..8];
        StorageBucket bucket1 = StorageBucket.Create(TenantId.New(), $"bucket1-{suffix}");
        DbContext.Buckets.Add(bucket1);
        await DbContext.SaveChangesAsync();

        StoredFile file1 = StoredFile.Create(TestTenantId, bucket1.Id, "tenant1.txt", "text/plain", 100, Guid.NewGuid().ToString(), Guid.NewGuid());
        _repository.Add(file1);
        await _repository.SaveChangesAsync();

        TenantId otherTenantId = TenantId.New();
        await using StorageDbContext otherDbContext = CreateDbContextForTenant(otherTenantId);
        StoredFileRepository otherRepository = new(otherDbContext);

        StorageBucket bucket2 = StorageBucket.Create(TenantId.New(), $"bucket2-{suffix}");
        otherDbContext.Buckets.Add(bucket2);
        await otherDbContext.SaveChangesAsync();

        StoredFile file2 = StoredFile.Create(otherTenantId, bucket2.Id, "tenant2.txt", "text/plain", 200, Guid.NewGuid().ToString(), Guid.NewGuid());
        otherRepository.Add(file2);
        await otherDbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();
        otherDbContext.ChangeTracker.Clear();

        IReadOnlyList<StoredFile> tenant1Results = await _repository.GetByBucketIdAsync(bucket1.Id);
        IReadOnlyList<StoredFile> tenant2Results = await otherRepository.GetByBucketIdAsync(bucket2.Id);

        tenant1Results.Should().HaveCount(1);
        tenant1Results[0].FileName.Should().Be("tenant1.txt");

        tenant2Results.Should().HaveCount(1);
        tenant2Results[0].FileName.Should().Be("tenant2.txt");
    }
}
