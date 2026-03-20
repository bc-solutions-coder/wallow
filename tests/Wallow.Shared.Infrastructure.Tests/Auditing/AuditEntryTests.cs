using System.Text.Json;
using Wallow.Shared.Infrastructure.Core.Auditing;

namespace Wallow.Shared.Infrastructure.Tests.Auditing;

public class AuditEntryTests
{
    [Fact]
    public void Constructor_DefaultId_IsEmptyGuid()
    {
        AuditEntry entry = new()
        {
            EntityType = "TestEntity",
            EntityId = "123",
            Action = "Insert"
        };

        entry.Id.Should().Be(Guid.Empty);
    }

    [Fact]
    public void Properties_WhenSet_ReturnCorrectValues()
    {
        Guid id = Guid.NewGuid();
        Guid tenantId = Guid.NewGuid();
        DateTimeOffset timestamp = DateTimeOffset.UtcNow;

        AuditEntry entry = new()
        {
            Id = id,
            EntityType = "Invoice",
            EntityId = "INV-001",
            Action = "Update",
            OldValues = """{"Amount":100}""",
            NewValues = """{"Amount":200}""",
            UserId = "user-42",
            TenantId = tenantId,
            Timestamp = timestamp
        };

        entry.Id.Should().Be(id);
        entry.EntityType.Should().Be("Invoice");
        entry.EntityId.Should().Be("INV-001");
        entry.Action.Should().Be("Update");
        entry.OldValues.Should().Be("""{"Amount":100}""");
        entry.NewValues.Should().Be("""{"Amount":200}""");
        entry.UserId.Should().Be("user-42");
        entry.TenantId.Should().Be(tenantId);
        entry.Timestamp.Should().Be(timestamp);
    }

    [Fact]
    public void OptionalProperties_WhenNotSet_AreNull()
    {
        AuditEntry entry = new()
        {
            EntityType = "TestEntity",
            EntityId = "1",
            Action = "Insert"
        };

        entry.OldValues.Should().BeNull();
        entry.NewValues.Should().BeNull();
        entry.UserId.Should().BeNull();
        entry.TenantId.Should().BeNull();
    }

    [Fact]
    public void NewValues_WithSerializedJson_DeserializesCorrectly()
    {
        Dictionary<string, object?> values = new()
        {
            ["Name"] = "Test",
            ["Amount"] = 42
        };
        string serialized = JsonSerializer.Serialize(values);

        AuditEntry entry = new()
        {
            EntityType = "Invoice",
            EntityId = "1",
            Action = "Insert",
            NewValues = serialized
        };

        Dictionary<string, JsonElement>? deserialized =
            JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(entry.NewValues);

        deserialized.Should().NotBeNull();
        deserialized["Name"].GetString().Should().Be("Test");
        deserialized["Amount"].GetInt32().Should().Be(42);
    }

    [Fact]
    public void OldValues_WithSerializedJson_DeserializesCorrectly()
    {
        Dictionary<string, object?> values = new()
        {
            ["Status"] = "Draft",
            ["Total"] = 99.99
        };
        string serialized = JsonSerializer.Serialize(values);

        AuditEntry entry = new()
        {
            EntityType = "Invoice",
            EntityId = "1",
            Action = "Update",
            OldValues = serialized
        };

        Dictionary<string, JsonElement>? deserialized =
            JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(entry.OldValues);

        deserialized.Should().NotBeNull();
        deserialized["Status"].GetString().Should().Be("Draft");
        deserialized["Total"].GetDouble().Should().BeApproximately(99.99, 0.001);
    }

    [Fact]
    public void Timestamp_DefaultValue_IsDefault()
    {
        AuditEntry entry = new()
        {
            EntityType = "TestEntity",
            EntityId = "1",
            Action = "Insert"
        };

        entry.Timestamp.Should().Be(default(DateTimeOffset));
    }
}
