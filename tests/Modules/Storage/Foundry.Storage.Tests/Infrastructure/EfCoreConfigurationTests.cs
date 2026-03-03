using Foundry.Shared.Kernel.Identity;
using Foundry.Shared.Kernel.MultiTenancy;
using Foundry.Storage.Domain.Entities;
using Foundry.Storage.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Foundry.Storage.Tests.Infrastructure;

public sealed class EfCoreConfigurationTests : IDisposable
{
    private readonly StorageDbContext _context;
    private readonly IModel _model;

    public EfCoreConfigurationTests()
    {
        TenantContext tenantContext = new TenantContext();
        tenantContext.SetTenant(TenantId.New(), "TestTenant");

        DbContextOptions<StorageDbContext> options = new DbContextOptionsBuilder<StorageDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        _context = new StorageDbContext(options, tenantContext);
        _model = _context.Model;
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    // StorageBucket configuration tests

    [Fact]
    public void StorageBucket_MapsToCorrectTable()
    {
        IEntityType? entityType = _model.FindEntityType(typeof(StorageBucket));

        entityType.Should().NotBeNull();
        entityType!.GetTableName().Should().Be("buckets");
    }

    [Fact]
    public void StorageBucket_HasCorrectColumnNames()
    {
        IEntityType? entityType = _model.FindEntityType(typeof(StorageBucket));
        entityType.Should().NotBeNull();

        entityType!.FindProperty("Name")!.GetColumnName().Should().Be("name");
        entityType.FindProperty("Description")!.GetColumnName().Should().Be("description");
        entityType.FindProperty("Access")!.GetColumnName().Should().Be("access");
        entityType.FindProperty("MaxFileSizeBytes")!.GetColumnName().Should().Be("max_file_size_bytes");
        entityType.FindProperty("AllowedContentTypes")!.GetColumnName().Should().Be("allowed_content_types");
        entityType.FindProperty("Versioning")!.GetColumnName().Should().Be("versioning");
        entityType.FindProperty("CreatedAt")!.GetColumnName().Should().Be("created_at");
    }

    [Fact]
    public void StorageBucket_Name_HasMaxLength100()
    {
        IEntityType? entityType = _model.FindEntityType(typeof(StorageBucket));

        int? maxLength = entityType!.FindProperty("Name")!.GetMaxLength();

        maxLength.Should().Be(100);
    }

    [Fact]
    public void StorageBucket_Description_HasMaxLength500()
    {
        IEntityType? entityType = _model.FindEntityType(typeof(StorageBucket));

        int? maxLength = entityType!.FindProperty("Description")!.GetMaxLength();

        maxLength.Should().Be(500);
    }

    [Fact]
    public void StorageBucket_Access_IsStoredAsString()
    {
        IEntityType? entityType = _model.FindEntityType(typeof(StorageBucket));
        IProperty? property = entityType!.FindProperty("Access");

        property!.GetMaxLength().Should().Be(20);
        property.GetProviderClrType().Should().Be<string>();
    }

    [Fact]
    public void StorageBucket_Id_IsValueGeneratedNever()
    {
        IEntityType? entityType = _model.FindEntityType(typeof(StorageBucket));

        entityType!.FindProperty("Id")!.ValueGenerated.Should().Be(ValueGenerated.Never);
    }

    [Fact]
    public void StorageBucket_HasTenantIdIndex()
    {
        IEntityType? entityType = _model.FindEntityType(typeof(StorageBucket));

        IEnumerable<IIndex> indexes = entityType!.GetIndexes();
        indexes.Should().Contain(i =>
            i.Properties.Any(p => p.Name == "TenantId"));
    }

    [Fact]
    public void StorageBucket_HasUniqueTenantNameIndex()
    {
        IEntityType? entityType = _model.FindEntityType(typeof(StorageBucket));

        IEnumerable<IIndex> indexes = entityType!.GetIndexes();
        IIndex? uniqueIndex = indexes.FirstOrDefault(i =>
            i.Properties.Count == 2 &&
            i.Properties.Any(p => p.Name == "TenantId") &&
            i.Properties.Any(p => p.Name == "Name") &&
            i.IsUnique);

        uniqueIndex.Should().NotBeNull();
    }

    // StoredFile configuration tests

    [Fact]
    public void StoredFile_MapsToCorrectTable()
    {
        IEntityType? entityType = _model.FindEntityType(typeof(StoredFile));

        entityType.Should().NotBeNull();
        entityType!.GetTableName().Should().Be("files");
    }

    [Fact]
    public void StoredFile_HasCorrectColumnNames()
    {
        IEntityType? entityType = _model.FindEntityType(typeof(StoredFile));
        entityType.Should().NotBeNull();

        entityType!.FindProperty("FileName")!.GetColumnName().Should().Be("file_name");
        entityType.FindProperty("ContentType")!.GetColumnName().Should().Be("content_type");
        entityType.FindProperty("SizeBytes")!.GetColumnName().Should().Be("size_bytes");
        entityType.FindProperty("StorageKey")!.GetColumnName().Should().Be("storage_key");
        entityType.FindProperty("Path")!.GetColumnName().Should().Be("path");
        entityType.FindProperty("IsPublic")!.GetColumnName().Should().Be("is_public");
        entityType.FindProperty("UploadedBy")!.GetColumnName().Should().Be("uploaded_by");
        entityType.FindProperty("UploadedAt")!.GetColumnName().Should().Be("uploaded_at");
        entityType.FindProperty("Metadata")!.GetColumnName().Should().Be("metadata");
    }

    [Fact]
    public void StoredFile_FileName_HasMaxLength500()
    {
        IEntityType? entityType = _model.FindEntityType(typeof(StoredFile));

        entityType!.FindProperty("FileName")!.GetMaxLength().Should().Be(500);
    }

    [Fact]
    public void StoredFile_StorageKey_HasMaxLength1000()
    {
        IEntityType? entityType = _model.FindEntityType(typeof(StoredFile));

        entityType!.FindProperty("StorageKey")!.GetMaxLength().Should().Be(1000);
    }

    [Fact]
    public void StoredFile_Id_IsValueGeneratedNever()
    {
        IEntityType? entityType = _model.FindEntityType(typeof(StoredFile));

        entityType!.FindProperty("Id")!.ValueGenerated.Should().Be(ValueGenerated.Never);
    }

    [Fact]
    public void StoredFile_HasStorageKeyUniqueIndex()
    {
        IEntityType? entityType = _model.FindEntityType(typeof(StoredFile));

        IEnumerable<IIndex> indexes = entityType!.GetIndexes();
        IIndex? uniqueIndex = indexes.FirstOrDefault(i =>
            i.Properties.Count == 1 &&
            i.Properties.Any(p => p.Name == "StorageKey") &&
            i.IsUnique);

        uniqueIndex.Should().NotBeNull();
    }

    [Fact]
    public void StoredFile_HasBucketIdIndex()
    {
        IEntityType? entityType = _model.FindEntityType(typeof(StoredFile));

        IEnumerable<IIndex> indexes = entityType!.GetIndexes();
        indexes.Should().Contain(i =>
            i.Properties.Count == 1 &&
            i.Properties.Any(p => p.Name == "BucketId"));
    }

    [Fact]
    public void StoredFile_HasBucketPathCompositeIndex()
    {
        IEntityType? entityType = _model.FindEntityType(typeof(StoredFile));

        IEnumerable<IIndex> indexes = entityType!.GetIndexes();
        IIndex? compositeIndex = indexes.FirstOrDefault(i =>
            i.Properties.Count == 2 &&
            i.Properties.Any(p => p.Name == "BucketId") &&
            i.Properties.Any(p => p.Name == "Path"));

        compositeIndex.Should().NotBeNull();
    }

    [Fact]
    public void StoredFile_HasForeignKeyToBucket_WithRestrictDelete()
    {
        IEntityType? entityType = _model.FindEntityType(typeof(StoredFile));

        IEnumerable<IForeignKey> foreignKeys = entityType!.GetForeignKeys();
        IForeignKey? bucketFk = foreignKeys.FirstOrDefault(fk =>
            fk.Properties.Any(p => p.Name == "BucketId"));

        bucketFk.Should().NotBeNull();
        bucketFk!.DeleteBehavior.Should().Be(DeleteBehavior.Restrict);
    }

    [Fact]
    public void StorageDbContext_UsesStorageSchema()
    {
        string? schema = _model.GetDefaultSchema();

        schema.Should().Be("storage");
    }
}
