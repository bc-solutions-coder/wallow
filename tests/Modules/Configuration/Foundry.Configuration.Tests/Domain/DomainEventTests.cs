using Foundry.Configuration.Domain.Enums;
using Foundry.Configuration.Domain.Events;
using Foundry.Shared.Kernel.CustomFields;

namespace Foundry.Configuration.Tests.Domain;

public class FeatureFlagCreatedEventTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        Guid flagId = Guid.NewGuid();

        FeatureFlagCreatedEvent evt = new(flagId, "dark_mode", FlagType.Boolean);

        evt.FlagId.Should().Be(flagId);
        evt.Key.Should().Be("dark_mode");
        evt.FlagType.Should().Be(FlagType.Boolean);
    }
}

public class FeatureFlagUpdatedEventTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        Guid flagId = Guid.NewGuid();

        FeatureFlagUpdatedEvent evt = new(flagId, "dark_mode", "Name,Description");

        evt.FlagId.Should().Be(flagId);
        evt.Key.Should().Be("dark_mode");
        evt.ChangedProperties.Should().Be("Name,Description");
    }
}

public class FeatureFlagDeletedEventTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        Guid flagId = Guid.NewGuid();

        FeatureFlagDeletedEvent evt = new(flagId, "dark_mode");

        evt.FlagId.Should().Be(flagId);
        evt.Key.Should().Be("dark_mode");
    }
}

public class FeatureFlagEvaluatedEventTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        Guid tenantId = Guid.NewGuid();
        Guid userId = Guid.NewGuid();
        DateTimeOffset timestamp = DateTimeOffset.UtcNow;

        FeatureFlagEvaluatedEvent evt = new(
            "dark_mode", tenantId, userId, "True", "Default value", timestamp);

        evt.FlagKey.Should().Be("dark_mode");
        evt.TenantId.Should().Be(tenantId);
        evt.UserId.Should().Be(userId);
        evt.Result.Should().Be("True");
        evt.Reason.Should().Be("Default value");
        evt.Timestamp.Should().Be(timestamp);
    }

    [Fact]
    public void Constructor_WithNullUserId_SetsNull()
    {
        FeatureFlagEvaluatedEvent evt = new(
            "feature", Guid.NewGuid(), null, "False", "reason", DateTimeOffset.UtcNow);

        evt.UserId.Should().BeNull();
    }
}

public class CustomFieldDefinitionCreatedEventTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        Guid definitionId = Guid.NewGuid();
        Guid tenantId = Guid.NewGuid();

        CustomFieldDefinitionCreatedEvent evt = new(
            definitionId, tenantId, "Invoice", "po_number", "PO Number", CustomFieldType.Text);

        evt.DefinitionId.Should().Be(definitionId);
        evt.TenantId.Should().Be(tenantId);
        evt.EntityType.Should().Be("Invoice");
        evt.FieldKey.Should().Be("po_number");
        evt.DisplayName.Should().Be("PO Number");
        evt.FieldType.Should().Be(CustomFieldType.Text);
    }
}

public class CustomFieldDefinitionDeactivatedEventTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        Guid definitionId = Guid.NewGuid();
        Guid tenantId = Guid.NewGuid();

        CustomFieldDefinitionDeactivatedEvent evt = new(
            definitionId, tenantId, "Invoice", "po_number");

        evt.DefinitionId.Should().Be(definitionId);
        evt.TenantId.Should().Be(tenantId);
        evt.EntityType.Should().Be("Invoice");
        evt.FieldKey.Should().Be("po_number");
    }
}
