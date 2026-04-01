using BenchmarkDotNet.Attributes;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Infrastructure.Persistence;
using Wallow.Identity.Infrastructure.Repositories;
using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.MultiTenancy;
using Wallow.Storage.Domain.Entities;
using Wallow.Storage.Domain.Identity;
using Wallow.Storage.Infrastructure.Persistence;
using Wallow.Storage.Infrastructure.Persistence.Repositories;

namespace Wallow.Benchmarks;

[MemoryDiagnoser]
[ShortRunJob]
public sealed class QueryBenchmarks : IDisposable
{
    private SqliteConnection _identityConnection = null!;
    private SqliteConnection _storageConnection = null!;

    private IdentityDbContext _identityDbContext = null!;
    private StorageDbContext _storageDbContext = null!;

    private ScimConfigurationRepository _scimConfigRepo = null!;
    private StorageBucketRepository _storageBucketRepo = null!;
    private StoredFileRepository _storedFileRepo = null!;

    private StoredFileId _testStoredFileId;
    private string _testBucketName = null!;

    [GlobalSetup]
    public void Setup()
    {
        BenchmarkTenantContext tenantContext = new();
        IDataProtectionProvider dataProtectionProvider = DataProtectionProvider.Create("Wallow.Benchmarks");

        // Identity
        _identityConnection = new SqliteConnection("DataSource=:memory:");
        _identityConnection.Open();
        DbContextOptions<IdentityDbContext> identityOptions = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseSqlite(_identityConnection)
            .Options;
        _identityDbContext = new IdentityDbContext(identityOptions, dataProtectionProvider);
        _identityDbContext.SetTenant(tenantContext.TenantId);
        _identityDbContext.Database.EnsureCreated();
        _scimConfigRepo = new ScimConfigurationRepository(_identityDbContext);


        // Storage
        _storageConnection = new SqliteConnection("DataSource=:memory:");
        _storageConnection.Open();
        DbContextOptions<StorageDbContext> storageOptions = new DbContextOptionsBuilder<StorageDbContext>()
            .UseSqlite(_storageConnection)
            .Options;
        _storageDbContext = new StorageDbContext(storageOptions);
        _storageDbContext.SetTenant(tenantContext.TenantId);
        _storageDbContext.Database.EnsureCreated();
        _storageBucketRepo = new StorageBucketRepository(_storageDbContext);
        _storedFileRepo = new StoredFileRepository(_storageDbContext);

        // Seed storage data
        _testBucketName = "bench-bucket";
        StorageBucket bucket = StorageBucket.Create(tenantContext.TenantId, _testBucketName);
        _storageDbContext.Buckets.Add(bucket);
        StoredFile storedFile = StoredFile.Create(
            tenantContext.TenantId, bucket.Id, "bench.txt", "text/plain", 100, "key/bench", Guid.NewGuid());
        _testStoredFileId = storedFile.Id;
        _storageDbContext.Files.Add(storedFile);
        _storageDbContext.SaveChanges();
        _storageDbContext.ChangeTracker.Clear();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        Dispose();
    }

    public void Dispose()
    {
        _identityDbContext.Dispose();
        _identityConnection.Dispose();
        _storageDbContext.Dispose();
        _storageConnection.Dispose();
        GC.SuppressFinalize(this);
    }

    [Benchmark]
    public Task<ScimConfiguration?> ScimConfiguration_Get()
    {
        _identityDbContext.ChangeTracker.Clear();
        return _scimConfigRepo.GetAsync();
    }

    [Benchmark]
    public Task<StorageBucket?> StorageBucket_GetByName()
    {
        _storageDbContext.ChangeTracker.Clear();
        return _storageBucketRepo.GetByNameAsync(_testBucketName);
    }

    [Benchmark]
    public Task<StoredFile?> StoredFile_GetById()
    {
        _storageDbContext.ChangeTracker.Clear();
        return _storedFileRepo.GetByIdAsync(_testStoredFileId);
    }

    private sealed class BenchmarkTenantContext : ITenantContext
    {
        public TenantId TenantId => TenantId.Create(Guid.Parse("00000000-0000-0000-0000-000000000001"));
        public string TenantName => "benchmark";
        public string Region => RegionConfiguration.PrimaryRegion;
        public bool IsResolved => true;
    }
}
