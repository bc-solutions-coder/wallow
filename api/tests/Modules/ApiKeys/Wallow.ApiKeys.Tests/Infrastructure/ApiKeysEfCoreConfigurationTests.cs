using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Wallow.ApiKeys.Domain.Entities;
using Wallow.ApiKeys.Infrastructure.Persistence;
using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.MultiTenancy;

namespace Wallow.ApiKeys.Tests.Infrastructure;

public sealed class ApiKeysEfCoreConfigurationTests : IDisposable
{
    private readonly ApiKeysDbContext _context;
    private readonly IModel _model;

    public ApiKeysEfCoreConfigurationTests()
    {
        TenantContext tenantContext = new TenantContext();
        tenantContext.SetTenant(TenantId.New(), "TestTenant");

        DbContextOptions<ApiKeysDbContext> options = new DbContextOptionsBuilder<ApiKeysDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        _context = new ApiKeysDbContext(options);
        _context.SetTenant(tenantContext.TenantId);
        _model = _context.Model;
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    [Fact]
    public void ApiKey_MapsToCorrectTable()
    {
        IEntityType? entityType = _model.FindEntityType(typeof(ApiKey));

        entityType.Should().NotBeNull();
        entityType!.GetTableName().Should().Be("api_keys");
    }

    [Fact]
    public void ApiKeysDbContext_UsesApiKeysSchema()
    {
        string? schema = _model.GetDefaultSchema();

        schema.Should().Be("apikeys");
    }

    [Fact]
    public void ApiKey_HasCorrectColumnNames()
    {
        IEntityType? entityType = _model.FindEntityType(typeof(ApiKey));
        entityType.Should().NotBeNull();

        entityType!.FindProperty("Id")!.GetColumnName().Should().Be("id");
        entityType.FindProperty("TenantId")!.GetColumnName().Should().Be("tenant_id");
        entityType.FindProperty("ServiceAccountId")!.GetColumnName().Should().Be("service_account_id");
        entityType.FindProperty("HashedKey")!.GetColumnName().Should().Be("hashed_key");
        entityType.FindProperty("DisplayName")!.GetColumnName().Should().Be("display_name");
        entityType.FindProperty("_scopes")!.GetColumnName().Should().Be("scopes");
        entityType.FindProperty("ExpiresAt")!.GetColumnName().Should().Be("expires_at");
        entityType.FindProperty("IsRevoked")!.GetColumnName().Should().Be("is_revoked");
        entityType.FindProperty("CreatedAt")!.GetColumnName().Should().Be("created_at");
        entityType.FindProperty("UpdatedAt")!.GetColumnName().Should().Be("updated_at");
        entityType.FindProperty("CreatedBy")!.GetColumnName().Should().Be("created_by");
        entityType.FindProperty("UpdatedBy")!.GetColumnName().Should().Be("updated_by");
    }

    [Fact]
    public void ApiKey_Id_HasValueConverter()
    {
        IEntityType? entityType = _model.FindEntityType(typeof(ApiKey));
        IProperty? idProperty = entityType!.FindProperty("Id");

        idProperty!.GetValueConverter().Should().NotBeNull();
    }

    [Fact]
    public void ApiKey_TenantId_HasValueConverter()
    {
        IEntityType? entityType = _model.FindEntityType(typeof(ApiKey));
        IProperty? tenantIdProperty = entityType!.FindProperty("TenantId");

        tenantIdProperty!.GetValueConverter().Should().NotBeNull();
    }

    [Fact]
    public void ApiKey_ServiceAccountId_HasMaxLength200_And_IsRequired()
    {
        IEntityType? entityType = _model.FindEntityType(typeof(ApiKey));
        IProperty? property = entityType!.FindProperty("ServiceAccountId");

        property!.GetMaxLength().Should().Be(200);
        property.IsNullable.Should().BeFalse();
    }

    [Fact]
    public void ApiKey_HashedKey_HasMaxLength128_And_IsRequired()
    {
        IEntityType? entityType = _model.FindEntityType(typeof(ApiKey));
        IProperty? property = entityType!.FindProperty("HashedKey");

        property!.GetMaxLength().Should().Be(128);
        property.IsNullable.Should().BeFalse();
    }

    [Fact]
    public void ApiKey_DisplayName_HasMaxLength100_And_IsRequired()
    {
        IEntityType? entityType = _model.FindEntityType(typeof(ApiKey));
        IProperty? property = entityType!.FindProperty("DisplayName");

        property!.GetMaxLength().Should().Be(100);
        property.IsNullable.Should().BeFalse();
    }

    [Fact]
    public void ApiKey_Scopes_HasColumnTypeJsonb()
    {
        IEntityType? entityType = _model.FindEntityType(typeof(ApiKey));
        IProperty? property = entityType!.FindProperty("_scopes");

        property!.GetColumnType().Should().Be("jsonb");
    }

    [Fact]
    public void ApiKey_HasIndexOnTenantId()
    {
        IEntityType? entityType = _model.FindEntityType(typeof(ApiKey));

        IEnumerable<IIndex> indexes = entityType!.GetIndexes();
        indexes.Should().Contain(i => i.Properties.Any(p => p.Name == "TenantId"));
    }

    [Fact]
    public void ApiKey_HasIndexOnServiceAccountId()
    {
        IEntityType? entityType = _model.FindEntityType(typeof(ApiKey));

        IEnumerable<IIndex> indexes = entityType!.GetIndexes();
        indexes.Should().Contain(i => i.Properties.Any(p => p.Name == "ServiceAccountId"));
    }

    [Fact]
    public void ApiKeysDbContext_SetsQueryTrackingBehaviorToNoTracking()
    {
        _context.ChangeTracker.QueryTrackingBehavior.Should().Be(QueryTrackingBehavior.NoTracking);
    }

    [Fact]
    public void ApiKeysDbContext_ExposesDbSetForApiKeys()
    {
        _context.ApiKeys.Should().NotBeNull();
    }

    [Fact]
    public void ApiKeysDbContext_AppliesTenantQueryFilter_ToApiKey()
    {
        IEntityType? entityType = _model.FindEntityType(typeof(ApiKey));

        entityType!.GetDeclaredQueryFilters().Should().NotBeEmpty();
    }
}
