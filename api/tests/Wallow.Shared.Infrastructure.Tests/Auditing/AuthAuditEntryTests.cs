using Microsoft.EntityFrameworkCore;
using Wallow.Shared.Infrastructure.Core.Auditing;
using Wallow.Shared.Kernel.Auditing;

namespace Wallow.Shared.Infrastructure.Tests.Auditing;

public class AuthAuditEntryTests
{
    private static AuthAuditDbContext CreateContext()
    {
        DbContextOptions<AuthAuditDbContext> options = new DbContextOptionsBuilder<AuthAuditDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;
        return new AuthAuditDbContext(options);
    }

    [Fact]
    public void Constructor_DefaultId_IsEmptyGuid()
    {
        AuthAuditEntry entry = new()
        {
            EventType = "LoginSuccess"
        };

        entry.Id.Should().Be(Guid.Empty);
    }

    [Fact]
    public void Properties_WhenSet_ReturnCorrectValues()
    {
        Guid id = Guid.NewGuid();
        Guid userId = Guid.NewGuid();
        Guid tenantId = Guid.NewGuid();
        DateTimeOffset occurredAt = DateTimeOffset.UtcNow;

        AuthAuditEntry entry = new()
        {
            Id = id,
            EventType = "LoginSuccess",
            UserId = userId,
            TenantId = tenantId,
            IpAddress = "192.168.1.1",
            UserAgent = "Mozilla/5.0",
            OccurredAt = occurredAt
        };

        entry.Id.Should().Be(id);
        entry.EventType.Should().Be("LoginSuccess");
        entry.UserId.Should().Be(userId);
        entry.TenantId.Should().Be(tenantId);
        entry.IpAddress.Should().Be("192.168.1.1");
        entry.UserAgent.Should().Be("Mozilla/5.0");
        entry.OccurredAt.Should().Be(occurredAt);
    }

    [Fact]
    public void OptionalProperties_WhenNotSet_AreNull()
    {
        AuthAuditEntry entry = new()
        {
            EventType = "LoginFailed"
        };

        entry.IpAddress.Should().BeNull();
        entry.UserAgent.Should().BeNull();
    }

    [Fact]
    public void OccurredAt_DefaultValue_IsDefault()
    {
        AuthAuditEntry entry = new()
        {
            EventType = "LoginSuccess"
        };

        entry.OccurredAt.Should().Be(default(DateTimeOffset));
    }

    [Fact]
    public void AuthAuditRecord_CanBeConstructedWithRequiredFields()
    {
        Guid userId = Guid.NewGuid();
        Guid tenantId = Guid.NewGuid();
        DateTimeOffset occurredAt = DateTimeOffset.UtcNow;

        AuthAuditRecord record = new()
        {
            EventType = "MfaEnabled",
            UserId = userId,
            TenantId = tenantId,
            IpAddress = "10.0.0.1",
            UserAgent = "TestAgent",
            OccurredAt = occurredAt
        };

        record.EventType.Should().Be("MfaEnabled");
        record.UserId.Should().Be(userId);
        record.TenantId.Should().Be(tenantId);
        record.IpAddress.Should().Be("10.0.0.1");
        record.UserAgent.Should().Be("TestAgent");
        record.OccurredAt.Should().Be(occurredAt);
    }

    [Fact]
    public void AuthAuditRecord_OptionalProperties_AreNullByDefault()
    {
        AuthAuditRecord record = new()
        {
            EventType = "LoginSuccess",
            UserId = Guid.NewGuid(),
            TenantId = Guid.NewGuid()
        };

        record.IpAddress.Should().BeNull();
        record.UserAgent.Should().BeNull();
    }

    [Fact]
    public void AuthAuditDbContext_Schema_IsAuthAudit()
    {
        using AuthAuditDbContext context = CreateContext();
        Microsoft.EntityFrameworkCore.Metadata.IEntityType? entityType = context.Model.FindEntityType(typeof(AuthAuditEntry));

        entityType.Should().NotBeNull();
        entityType!.GetSchema().Should().Be("auth_audit");
        entityType.GetTableName().Should().Be("auth_audit_entries");
    }

    [Fact]
    public void AuthAuditDbContext_OccurredAt_HasDefaultValueSql()
    {
        using AuthAuditDbContext context = CreateContext();
        Microsoft.EntityFrameworkCore.Metadata.IProperty? property = context.Model
            .FindEntityType(typeof(AuthAuditEntry))!
            .FindProperty(nameof(AuthAuditEntry.OccurredAt));

        property.Should().NotBeNull();
        property!.GetDefaultValueSql().Should().Be("now()");
    }

    [Fact]
    public void AuthAuditDbContext_HasPrimaryKey_OnId()
    {
        using AuthAuditDbContext context = CreateContext();
        Microsoft.EntityFrameworkCore.Metadata.IEntityType? entityType = context.Model.FindEntityType(typeof(AuthAuditEntry));

        entityType.Should().NotBeNull();
        Microsoft.EntityFrameworkCore.Metadata.IKey? primaryKey = entityType!.FindPrimaryKey();
        primaryKey.Should().NotBeNull();
        primaryKey!.Properties.Should().HaveCount(1);
        primaryKey.Properties[0].Name.Should().Be(nameof(AuthAuditEntry.Id));
    }
}
