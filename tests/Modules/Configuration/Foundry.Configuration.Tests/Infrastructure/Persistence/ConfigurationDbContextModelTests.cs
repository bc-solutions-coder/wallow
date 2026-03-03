using Foundry.Configuration.Domain.Entities;
using Foundry.Configuration.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Foundry.Configuration.Tests.Infrastructure.Persistence;

public class ConfigurationDbContextModelTests
{
    private static ConfigurationDbContext CreateContext()
    {
        ConfigurationDbContextFactory factory = new();
        return factory.CreateDbContext([]);
    }

    [Fact]
    public void Model_HasConfigurationSchema()
    {
        using ConfigurationDbContext context = CreateContext();

        string? schema = context.Model.GetDefaultSchema();

        schema.Should().Be("configuration");
    }

    [Fact]
    public void CustomFieldDefinitions_HasCorrectTableName()
    {
        using ConfigurationDbContext context = CreateContext();

        IEntityType? entityType = context.Model.FindEntityType(typeof(CustomFieldDefinition));

        entityType.Should().NotBeNull();
        entityType.GetTableName().Should().Be("custom_field_definitions");
    }

    [Fact]
    public void FeatureFlags_HasCorrectTableName()
    {
        using ConfigurationDbContext context = CreateContext();

        IEntityType? entityType = context.Model.FindEntityType(typeof(FeatureFlag));

        entityType.Should().NotBeNull();
        entityType.GetTableName().Should().Be("feature_flags");
    }

    [Fact]
    public void FeatureFlagOverrides_HasCorrectTableName()
    {
        using ConfigurationDbContext context = CreateContext();

        IEntityType? entityType = context.Model.FindEntityType(typeof(FeatureFlagOverride));

        entityType.Should().NotBeNull();
        entityType.GetTableName().Should().Be("feature_flag_overrides");
    }

    [Fact]
    public void FeatureFlag_Key_HasUniqueIndex()
    {
        using ConfigurationDbContext context = CreateContext();

        IEntityType? entityType = context.Model.FindEntityType(typeof(FeatureFlag));
        entityType.Should().NotBeNull();
        IProperty? keyProperty = entityType.FindProperty(nameof(FeatureFlag.Key));

        keyProperty.Should().NotBeNull();

        IIndex? uniqueIndex = entityType.GetIndexes()
            .FirstOrDefault(i => i.Properties.Any(p => p.Name == nameof(FeatureFlag.Key)));
        uniqueIndex.Should().NotBeNull();
        uniqueIndex.IsUnique.Should().BeTrue();
    }

    [Fact]
    public void CustomFieldDefinition_HasCompositeUniqueIndex()
    {
        using ConfigurationDbContext context = CreateContext();

        IEntityType? entityType = context.Model.FindEntityType(typeof(CustomFieldDefinition));
        IIndex? uniqueIndex = entityType?.GetIndexes()
            .FirstOrDefault(i => i.GetDatabaseName() == "ix_custom_field_definitions_tenant_entity_key");

        uniqueIndex.Should().NotBeNull();
        uniqueIndex.IsUnique.Should().BeTrue();
    }

    [Fact]
    public void FeatureFlagOverride_HasCascadeDeleteFromFlag()
    {
        using ConfigurationDbContext context = CreateContext();

        IEntityType? overrideType = context.Model.FindEntityType(typeof(FeatureFlagOverride));
        IForeignKey? fk = overrideType?.GetForeignKeys()
            .FirstOrDefault(f => f.PrincipalEntityType.ClrType == typeof(FeatureFlag));

        fk.Should().NotBeNull();
        fk.DeleteBehavior.Should().Be(DeleteBehavior.Cascade);
    }

    [Fact]
    public void FeatureFlag_Key_HasMaxLength100()
    {
        using ConfigurationDbContext context = CreateContext();

        IEntityType? entityType = context.Model.FindEntityType(typeof(FeatureFlag));
        IProperty? keyProp = entityType?.FindProperty(nameof(FeatureFlag.Key));

        keyProp.Should().NotBeNull();
        keyProp.GetMaxLength().Should().Be(100);
    }

    [Fact]
    public void CustomFieldDefinition_FieldKey_HasMaxLength50()
    {
        using ConfigurationDbContext context = CreateContext();

        IEntityType? entityType = context.Model.FindEntityType(typeof(CustomFieldDefinition));
        IProperty? prop = entityType?.FindProperty(nameof(CustomFieldDefinition.FieldKey));

        prop.Should().NotBeNull();
        prop.GetMaxLength().Should().Be(50);
    }

    [Fact]
    public void CustomFieldDefinition_DomainEvents_IsIgnored()
    {
        using ConfigurationDbContext context = CreateContext();

        IEntityType? entityType = context.Model.FindEntityType(typeof(CustomFieldDefinition));
        IProperty? prop = entityType?.FindProperty("DomainEvents");

        prop.Should().BeNull("DomainEvents should be ignored in EF configuration");
    }

    [Fact]
    public void FeatureFlagOverride_ExpiresAt_IsMapped()
    {
        using ConfigurationDbContext context = CreateContext();

        IEntityType? entityType = context.Model.FindEntityType(typeof(FeatureFlagOverride));
        IProperty? prop = entityType?.FindProperty(nameof(FeatureFlagOverride.ExpiresAt));

        prop.Should().NotBeNull();
        prop.GetColumnName().Should().Be("expires_at");
    }

    [Fact]
    public void FeatureFlag_HasVariantsOwnedCollection()
    {
        using ConfigurationDbContext context = CreateContext();

        IEntityType? flagType = context.Model.FindEntityType(typeof(FeatureFlag));
        INavigation? variantsNav = flagType?.GetNavigations()
            .FirstOrDefault(n => n.Name == nameof(FeatureFlag.Variants));

        variantsNav.Should().NotBeNull();
    }
}
