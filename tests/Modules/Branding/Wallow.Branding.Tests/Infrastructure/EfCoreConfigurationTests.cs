using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Wallow.Branding.Domain.Entities;
using Wallow.Branding.Infrastructure.Persistence;
using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.MultiTenancy;

namespace Wallow.Branding.Tests.Infrastructure;

public sealed class EfCoreConfigurationTests : IDisposable
{
    private readonly BrandingDbContext _context;
    private readonly IModel _model;

    public EfCoreConfigurationTests()
    {
        TenantContext tenantContext = new TenantContext();
        tenantContext.SetTenant(TenantId.New(), "TestTenant");

        DbContextOptions<BrandingDbContext> options = new DbContextOptionsBuilder<BrandingDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        _context = new BrandingDbContext(options);
        _context.SetTenant(tenantContext.TenantId);
        _model = _context.Model;
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    [Fact]
    public void ClientBranding_MapsToCorrectTable()
    {
        IEntityType? entityType = _model.FindEntityType(typeof(ClientBranding));

        entityType.Should().NotBeNull();
        entityType!.GetTableName().Should().Be("client_brandings");
    }

    [Fact]
    public void ClientBranding_HasCorrectColumnNames()
    {
        IEntityType? entityType = _model.FindEntityType(typeof(ClientBranding));
        entityType.Should().NotBeNull();

        entityType!.FindProperty("ClientId")!.GetColumnName().Should().Be("client_id");
        entityType.FindProperty("DisplayName")!.GetColumnName().Should().Be("display_name");
        entityType.FindProperty("Tagline")!.GetColumnName().Should().Be("tagline");
        entityType.FindProperty("LogoStorageKey")!.GetColumnName().Should().Be("logo_storage_key");
        entityType.FindProperty("ThemeJson")!.GetColumnName().Should().Be("theme_json");
        entityType.FindProperty("TenantId")!.GetColumnName().Should().Be("tenant_id");
        entityType.FindProperty("CreatedAt")!.GetColumnName().Should().Be("created_at");
        entityType.FindProperty("UpdatedAt")!.GetColumnName().Should().Be("updated_at");
    }

    [Fact]
    public void ClientBranding_ClientId_HasMaxLength200()
    {
        IEntityType? entityType = _model.FindEntityType(typeof(ClientBranding));

        int? maxLength = entityType!.FindProperty("ClientId")!.GetMaxLength();

        maxLength.Should().Be(200);
    }

    [Fact]
    public void ClientBranding_DisplayName_HasMaxLength200()
    {
        IEntityType? entityType = _model.FindEntityType(typeof(ClientBranding));

        int? maxLength = entityType!.FindProperty("DisplayName")!.GetMaxLength();

        maxLength.Should().Be(200);
    }

    [Fact]
    public void ClientBranding_Tagline_HasMaxLength500()
    {
        IEntityType? entityType = _model.FindEntityType(typeof(ClientBranding));

        int? maxLength = entityType!.FindProperty("Tagline")!.GetMaxLength();

        maxLength.Should().Be(500);
    }

    [Fact]
    public void ClientBranding_LogoStorageKey_HasMaxLength500()
    {
        IEntityType? entityType = _model.FindEntityType(typeof(ClientBranding));

        int? maxLength = entityType!.FindProperty("LogoStorageKey")!.GetMaxLength();

        maxLength.Should().Be(500);
    }

    [Fact]
    public void ClientBranding_ClientId_IsRequired()
    {
        IEntityType? entityType = _model.FindEntityType(typeof(ClientBranding));

        bool isRequired = !entityType!.FindProperty("ClientId")!.IsNullable;

        isRequired.Should().BeTrue();
    }

    [Fact]
    public void ClientBranding_DisplayName_IsRequired()
    {
        IEntityType? entityType = _model.FindEntityType(typeof(ClientBranding));

        bool isRequired = !entityType!.FindProperty("DisplayName")!.IsNullable;

        isRequired.Should().BeTrue();
    }

    [Fact]
    public void ClientBranding_TenantId_IsRequired()
    {
        IEntityType? entityType = _model.FindEntityType(typeof(ClientBranding));

        bool isRequired = !entityType!.FindProperty("TenantId")!.IsNullable;

        isRequired.Should().BeTrue();
    }

    [Fact]
    public void ClientBranding_HasUniqueClientIdIndex()
    {
        IEntityType? entityType = _model.FindEntityType(typeof(ClientBranding));

        IEnumerable<IIndex> indexes = entityType!.GetIndexes();
        IIndex? uniqueIndex = indexes.FirstOrDefault(i =>
            i.Properties.Count == 1 &&
            i.Properties.Any(p => p.Name == "ClientId") &&
            i.IsUnique);

        uniqueIndex.Should().NotBeNull();
    }

    [Fact]
    public void ClientBranding_HasTenantIdIndex()
    {
        IEntityType? entityType = _model.FindEntityType(typeof(ClientBranding));

        IEnumerable<IIndex> indexes = entityType!.GetIndexes();
        indexes.Should().Contain(i =>
            i.Properties.Any(p => p.Name == "TenantId"));
    }

    [Fact]
    public void ClientBranding_Id_HasValueConversion()
    {
        IEntityType? entityType = _model.FindEntityType(typeof(ClientBranding));
        IProperty? idProperty = entityType!.FindProperty("Id");

        idProperty!.GetValueConverter().Should().NotBeNull();
    }

    [Fact]
    public void ClientBranding_TenantId_HasValueConversion()
    {
        IEntityType? entityType = _model.FindEntityType(typeof(ClientBranding));
        IProperty? tenantIdProperty = entityType!.FindProperty("TenantId");

        tenantIdProperty!.GetValueConverter().Should().NotBeNull();
    }

    [Fact]
    public void BrandingDbContext_UsesBrandingSchema()
    {
        string? schema = _model.GetDefaultSchema();

        schema.Should().Be("branding");
    }

    [Fact]
    public void BrandingDbContext_SetsQueryTrackingBehaviorToNoTracking()
    {
        _context.ChangeTracker.QueryTrackingBehavior.Should().Be(QueryTrackingBehavior.NoTracking);
    }

    [Fact]
    public void BrandingDbContext_ExposesDbSetForClientBrandings()
    {
        _context.ClientBrandings.Should().NotBeNull();
    }

    [Fact]
    public void BrandingDbContext_AppliesTenantQueryFilter_ToClientBranding()
    {
        IEntityType? entityType = _model.FindEntityType(typeof(ClientBranding));

        entityType!.GetDeclaredQueryFilters().Should().NotBeEmpty();
    }
}
