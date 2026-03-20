using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.Pagination;
using Wallow.Storage.Domain.Entities;
using Wallow.Storage.Infrastructure.Persistence;
using Wallow.Storage.Infrastructure.Persistence.Repositories;
using Wallow.Tests.Common.Bases;
using Wallow.Tests.Common.Fixtures;

namespace Wallow.Storage.Tests.Integration;

[Collection("PostgresDatabase")]
[Trait("Category", "Integration")]
public sealed class StoredFileRepositoryPagedTests(PostgresContainerFixture fixture)
    : DbContextIntegrationTestBase<StorageDbContext>(fixture)
{
    private StoredFileRepository _repository = null!;

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _repository = new StoredFileRepository(DbContext);
    }

    [Fact]
    public async Task GetByBucketIdPagedAsync_ReturnsAllFilesForTenant()
    {
        StorageBucket bucket = StorageBucket.Create(TenantId.New(), $"paged-bucket-{Guid.NewGuid()}");
        DbContext.Buckets.Add(bucket);
        await DbContext.SaveChangesAsync();

        StoredFile file1 = StoredFile.Create(TestTenantId, bucket.Id, "file1.txt", "text/plain", 100, Guid.NewGuid().ToString(), Guid.NewGuid());
        StoredFile file2 = StoredFile.Create(TestTenantId, bucket.Id, "file2.txt", "text/plain", 200, Guid.NewGuid().ToString(), Guid.NewGuid());
        StoredFile file3 = StoredFile.Create(TestTenantId, bucket.Id, "file3.txt", "text/plain", 300, Guid.NewGuid().ToString(), Guid.NewGuid());
        _repository.Add(file1);
        _repository.Add(file2);
        _repository.Add(file3);
        await _repository.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        PagedResult<StoredFile> result = await _repository.GetByBucketIdPagedAsync(
            bucket.Id, TestTenantId.Value, null, page: 1, pageSize: 20);

        result.TotalCount.Should().Be(3);
        result.Items.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetByBucketIdPagedAsync_WithPaging_ReturnsCorrectPage()
    {
        StorageBucket bucket = StorageBucket.Create(TenantId.New(), $"paged-bucket-{Guid.NewGuid()}");
        DbContext.Buckets.Add(bucket);
        await DbContext.SaveChangesAsync();

        for (int i = 0; i < 5; i++)
        {
            StoredFile file = StoredFile.Create(TestTenantId, bucket.Id, $"file{i}.txt", "text/plain", 100, Guid.NewGuid().ToString(), Guid.NewGuid());
            _repository.Add(file);
        }
        await _repository.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        PagedResult<StoredFile> page1 = await _repository.GetByBucketIdPagedAsync(
            bucket.Id, TestTenantId.Value, null, page: 1, pageSize: 2);

        PagedResult<StoredFile> page2 = await _repository.GetByBucketIdPagedAsync(
            bucket.Id, TestTenantId.Value, null, page: 2, pageSize: 2);

        page1.TotalCount.Should().Be(5);
        page1.Items.Should().HaveCount(2);
        page2.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetByBucketIdPagedAsync_WithPathPrefix_FiltersResults()
    {
        StorageBucket bucket = StorageBucket.Create(TenantId.New(), $"prefix-bucket-{Guid.NewGuid()}");
        DbContext.Buckets.Add(bucket);
        await DbContext.SaveChangesAsync();

        StoredFile doc1 = StoredFile.Create(TestTenantId, bucket.Id, "d1.pdf", "application/pdf", 100, Guid.NewGuid().ToString(), Guid.NewGuid(), "docs/");
        StoredFile doc2 = StoredFile.Create(TestTenantId, bucket.Id, "d2.pdf", "application/pdf", 200, Guid.NewGuid().ToString(), Guid.NewGuid(), "docs/");
        StoredFile img = StoredFile.Create(TestTenantId, bucket.Id, "img.png", "image/png", 300, Guid.NewGuid().ToString(), Guid.NewGuid(), "images/");
        _repository.Add(doc1);
        _repository.Add(doc2);
        _repository.Add(img);
        await _repository.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        PagedResult<StoredFile> result = await _repository.GetByBucketIdPagedAsync(
            bucket.Id, TestTenantId.Value, "docs/", page: 1, pageSize: 20);

        result.TotalCount.Should().Be(2);
        result.Items.Should().HaveCount(2);
        result.Items.Should().AllSatisfy(f => f.Path!.StartsWith("docs/", StringComparison.Ordinal).Should().BeTrue());
    }

    [Fact]
    public async Task GetByBucketIdPagedAsync_OnlyReturnsSameTenantFiles()
    {
        string suffix = Guid.NewGuid().ToString()[..8];
        StorageBucket bucket = StorageBucket.Create(TenantId.New(), $"isolation-{suffix}");
        DbContext.Buckets.Add(bucket);
        await DbContext.SaveChangesAsync();

        StoredFile myFile = StoredFile.Create(TestTenantId, bucket.Id, "mine.txt", "text/plain", 100, Guid.NewGuid().ToString(), Guid.NewGuid());
        _repository.Add(myFile);
        await _repository.SaveChangesAsync();

        // Add a file for a different tenant using a separate context
        TenantId otherTenant = TenantId.New();
        await using StorageDbContext otherContext = CreateDbContextForTenant(otherTenant);
        StoredFileRepository otherRepository = new(otherContext);
        StoredFile otherFile = StoredFile.Create(otherTenant, bucket.Id, "other.txt", "text/plain", 200, Guid.NewGuid().ToString(), Guid.NewGuid());
        otherRepository.Add(otherFile);
        await otherContext.SaveChangesAsync();

        DbContext.ChangeTracker.Clear();

        PagedResult<StoredFile> result = await _repository.GetByBucketIdPagedAsync(
            bucket.Id, TestTenantId.Value, null, page: 1, pageSize: 20);

        result.TotalCount.Should().Be(1);
        result.Items.Should().HaveCount(1);
        result.Items[0].FileName.Should().Be("mine.txt");
    }

    [Fact]
    public async Task GetByBucketIdPagedAsync_WhenEmpty_ReturnsZeroCount()
    {
        StorageBucket bucket = StorageBucket.Create(TenantId.New(), $"empty-bucket-{Guid.NewGuid()}");
        DbContext.Buckets.Add(bucket);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        PagedResult<StoredFile> result = await _repository.GetByBucketIdPagedAsync(
            bucket.Id, TestTenantId.Value, null, page: 1, pageSize: 20);

        result.TotalCount.Should().Be(0);
        result.Items.Should().BeEmpty();
    }
}
