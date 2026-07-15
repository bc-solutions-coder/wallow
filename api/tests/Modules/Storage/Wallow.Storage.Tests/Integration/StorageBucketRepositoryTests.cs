using Wallow.Shared.Kernel.Identity;
using Wallow.Storage.Domain.Entities;
using Wallow.Storage.Infrastructure.Persistence;
using Wallow.Storage.Infrastructure.Persistence.Repositories;
using Wallow.Tests.Common.Bases;
using Wallow.Tests.Common.Fixtures;

namespace Wallow.Storage.Tests.Integration;

[Collection("PostgresDatabase")]
[Trait("Category", "Integration")]
public sealed class StorageBucketRepositoryTests : DbContextIntegrationTestBase<StorageDbContext>
{
    private StorageBucketRepository _repository = null!;

    public StorageBucketRepositoryTests(PostgresContainerFixture fixture) : base(fixture) { }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _repository = new StorageBucketRepository(DbContext);
    }

    [Fact]
    public async Task Add_PersistsBucket()
    {
        StorageBucket bucket = StorageBucket.Create(TenantId.New(), $"test-bucket-{Guid.NewGuid()}");

        _repository.Add(bucket);
        await _repository.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        StorageBucket? retrieved = await _repository.GetByNameAsync(bucket.Name);

        retrieved.Should().NotBeNull();
        retrieved!.Name.Should().Be(bucket.Name);
    }

    [Fact]
    public async Task GetByNameAsync_WhenExists_ReturnsBucket()
    {
        string bucketName = $"get-by-name-{Guid.NewGuid()}";
        StorageBucket bucket = StorageBucket.Create(TenantId.New(), bucketName, "test description");

        _repository.Add(bucket);
        await _repository.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        StorageBucket? retrieved = await _repository.GetByNameAsync(bucketName);

        retrieved.Should().NotBeNull();
        retrieved!.Name.Should().Be(bucketName);
        retrieved.Description.Should().Be("test description");
    }

    [Fact]
    public async Task GetByNameAsync_WhenNotExists_ReturnsNull()
    {
        StorageBucket? retrieved = await _repository.GetByNameAsync($"nonexistent-{Guid.NewGuid()}");

        retrieved.Should().BeNull();
    }

    [Fact]
    public async Task ExistsByNameAsync_WhenExists_ReturnsTrue()
    {
        string bucketName = $"exists-check-{Guid.NewGuid()}";
        StorageBucket bucket = StorageBucket.Create(TenantId.New(), bucketName);

        _repository.Add(bucket);
        await _repository.SaveChangesAsync();

        bool exists = await _repository.ExistsByNameAsync(bucketName);

        exists.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsByNameAsync_WhenNotExists_ReturnsFalse()
    {
        bool exists = await _repository.ExistsByNameAsync($"nonexistent-{Guid.NewGuid()}");

        exists.Should().BeFalse();
    }

    [Fact]
    public async Task Remove_DeletesBucket()
    {
        string bucketName = $"remove-bucket-{Guid.NewGuid()}";
        StorageBucket bucket = StorageBucket.Create(TenantId.New(), bucketName);

        _repository.Add(bucket);
        await _repository.SaveChangesAsync();

        StorageBucket? toRemove = await _repository.GetByNameAsync(bucketName);
        toRemove.Should().NotBeNull();

        _repository.Remove(toRemove!);
        await _repository.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        StorageBucket? retrieved = await _repository.GetByNameAsync(bucketName);
        retrieved.Should().BeNull();
    }

    [Fact]
    public async Task GetByNameAsync_ReturnsTrackedEntity()
    {
        string bucketName = $"tracked-{Guid.NewGuid()}";
        StorageBucket bucket = StorageBucket.Create(TenantId.New(), bucketName);

        _repository.Add(bucket);
        await _repository.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        StorageBucket? retrieved = await _repository.GetByNameAsync(bucketName);

        retrieved.Should().NotBeNull();
        DbContext.Entry(retrieved!).State.Should().Be(Microsoft.EntityFrameworkCore.EntityState.Unchanged);
    }
}
