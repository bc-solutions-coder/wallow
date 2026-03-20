using Wallow.Shared.Kernel.Domain;
using Wallow.Shared.Kernel.Identity;

namespace Wallow.Shared.Kernel.Tests.Domain;

public class AuditableEntityTests
{
    [Fact]
    public void SetCreated_WithUserId_SetsCreatedAtAndCreatedBy()
    {
        Guid userId = Guid.NewGuid();
        TestAuditableEntity entity = new(TestAuditableEntityId.New());
        DateTimeOffset timestamp = DateTimeOffset.UtcNow;

        entity.SetCreated(timestamp, userId);

        entity.CreatedAt.Should().Be(timestamp.UtcDateTime);
        entity.CreatedBy.Should().Be(userId);
    }

    [Fact]
    public void SetCreated_WithoutUserId_SetsCreatedAtAndNullCreatedBy()
    {
        TestAuditableEntity entity = new(TestAuditableEntityId.New());
        DateTimeOffset timestamp = DateTimeOffset.UtcNow;

        entity.SetCreated(timestamp);

        entity.CreatedAt.Should().Be(timestamp.UtcDateTime);
        entity.CreatedBy.Should().BeNull();
    }

    [Fact]
    public void SetUpdated_WithUserId_SetsUpdatedAtAndUpdatedBy()
    {
        Guid userId = Guid.NewGuid();
        TestAuditableEntity entity = new(TestAuditableEntityId.New());
        DateTimeOffset timestamp = DateTimeOffset.UtcNow;

        entity.SetUpdated(timestamp, userId);

        entity.UpdatedAt.Should().Be(timestamp.UtcDateTime);
        entity.UpdatedBy.Should().Be(userId);
    }

    [Fact]
    public void SetUpdated_WithoutUserId_SetsUpdatedAtAndNullUpdatedBy()
    {
        TestAuditableEntity entity = new(TestAuditableEntityId.New());
        DateTimeOffset timestamp = DateTimeOffset.UtcNow;

        entity.SetUpdated(timestamp);

        entity.UpdatedAt.Should().Be(timestamp.UtcDateTime);
        entity.UpdatedBy.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithId_SetsIdAndDefaultAuditFields()
    {
        TestAuditableEntityId id = TestAuditableEntityId.New();

        TestAuditableEntity entity = new(id);

        entity.Id.Should().Be(id);
        entity.CreatedAt.Should().Be(default);
        entity.UpdatedAt.Should().BeNull();
        entity.CreatedBy.Should().BeNull();
        entity.UpdatedBy.Should().BeNull();
    }

    [Fact]
    public void SetUpdated_WithNullUserId_SetsUpdatedAtButLeavesUpdatedByNull()
    {
        TestAuditableEntity entity = new(TestAuditableEntityId.New());
        DateTimeOffset timestamp = DateTimeOffset.UtcNow;

        entity.SetUpdated(timestamp);

        entity.UpdatedAt.Should().Be(timestamp.UtcDateTime);
        entity.UpdatedBy.Should().BeNull();
    }

    [Fact]
    public void ParameterlessCtor_InitializesWithDefaultValues()
    {
        TestAuditableEntity entity = new();

        entity.Id.Should().Be(default(TestAuditableEntityId));
        entity.CreatedAt.Should().Be(default);
        entity.UpdatedAt.Should().BeNull();
        entity.CreatedBy.Should().BeNull();
        entity.UpdatedBy.Should().BeNull();
    }

    private sealed class TestAuditableEntity : AuditableEntity<TestAuditableEntityId>
    {
        public TestAuditableEntity() { }
        public TestAuditableEntity(TestAuditableEntityId id) : base(id) { }
    }

    private readonly record struct TestAuditableEntityId(Guid Value) : IStronglyTypedId<TestAuditableEntityId>
    {
        public static TestAuditableEntityId Create(Guid value) => new(value);
        public static TestAuditableEntityId New() => new(Guid.NewGuid());
    }
}
