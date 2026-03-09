using Foundry.Shared.Kernel.Domain;
using Foundry.Shared.Kernel.Identity;

namespace Foundry.Shared.Kernel.Tests.Domain;

public class EntityTests
{
    [Fact]
    public void Constructor_WithId_SetsId()
    {
        TestEntityId id = TestEntityId.New();

        TestEntity entity = new(id);

        entity.Id.Should().Be(id);
    }

    [Fact]
    public void Equals_SameId_ReturnsTrue()
    {
        TestEntityId id = TestEntityId.New();
        TestEntity entity1 = new(id);
        TestEntity entity2 = new(id);

        entity1.Equals(entity2).Should().BeTrue();
        (entity1 == entity2).Should().BeTrue();
    }

    [Fact]
    public void Equals_DifferentId_ReturnsFalse()
    {
        TestEntity entity1 = new(TestEntityId.New());
        TestEntity entity2 = new(TestEntityId.New());

        entity1.Equals(entity2).Should().BeFalse();
        (entity1 != entity2).Should().BeTrue();
    }

    [Fact]
    public void Equals_Null_ReturnsFalse()
    {
        TestEntity entity = new(TestEntityId.New());

        // Intentionally testing null equality behavior
#pragma warning disable CA1508 // Avoid dead conditional code
        entity.Equals(null).Should().BeFalse();
#pragma warning restore CA1508
    }

    [Fact]
    public void Equals_SameReference_ReturnsTrue()
    {
        TestEntity entity = new(TestEntityId.New());

        entity.Equals(entity).Should().BeTrue();
        // Intentionally testing same-reference equality
#pragma warning disable CS1718 // Comparison made to same variable
        // ReSharper disable once EqualExpressionComparison
        (entity == entity).Should().BeTrue();
#pragma warning restore CS1718
    }

    [Fact]
    public void GetHashCode_SameId_ReturnsSameHash()
    {
        TestEntityId id = TestEntityId.New();
        TestEntity entity1 = new(id);
        TestEntity entity2 = new(id);

        entity1.GetHashCode().Should().Be(entity2.GetHashCode());
    }

    [Fact]
    public void GetHashCode_DifferentId_ReturnsDifferentHash()
    {
        TestEntity entity1 = new(TestEntityId.New());
        TestEntity entity2 = new(TestEntityId.New());

        entity1.GetHashCode().Should().NotBe(entity2.GetHashCode());
    }

    [Fact]
    public void OperatorEquals_BothNull_ReturnsTrue()
    {
        TestEntity? entity1 = null;
        TestEntity? entity2 = null;

        // Intentionally testing null-null equality
#pragma warning disable CA1508 // Avoid dead conditional code
        (entity1 == entity2).Should().BeTrue();
#pragma warning restore CA1508
    }

    [Fact]
    public void OperatorEquals_OneNull_ReturnsFalse()
    {
        TestEntity entity = new(TestEntityId.New());
        TestEntity? nullEntity = null;

        // Intentionally testing entity-null equality
#pragma warning disable CA1508 // Avoid dead conditional code
        (entity == nullEntity).Should().BeFalse();
        (nullEntity == entity).Should().BeFalse();
#pragma warning restore CA1508
    }

    [Fact]
    public void OperatorNotEquals_DifferentId_ReturnsTrue()
    {
        TestEntity entity1 = new(TestEntityId.New());
        TestEntity entity2 = new(TestEntityId.New());

        (entity1 != entity2).Should().BeTrue();
    }

    [Fact]
    public void OperatorNotEquals_SameId_ReturnsFalse()
    {
        TestEntityId id = TestEntityId.New();
        TestEntity entity1 = new(id);
        TestEntity entity2 = new(id);

        (entity1 != entity2).Should().BeFalse();
    }

    [Fact]
    public void Equals_Object_DifferentType_ReturnsFalse()
    {
        TestEntity entity = new(TestEntityId.New());
        object obj = new();

        entity.Equals(obj).Should().BeFalse();
    }

    private sealed class TestEntity : Entity<TestEntityId>
    {
        public TestEntity(TestEntityId id) : base(id) { }
    }

    private readonly record struct TestEntityId(Guid Value) : IStronglyTypedId<TestEntityId>
    {
        public static TestEntityId Create(Guid value) => new(value);
        public static TestEntityId New() => new(Guid.NewGuid());
    }
}
