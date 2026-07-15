using Wallow.Shared.Kernel.Domain;

namespace Wallow.Shared.Kernel.Tests.Domain;

public class ValueObjectTests
{
    [Fact]
    public void Equals_SameValues_ReturnsTrue()
    {
        TestValueObject vo1 = new("John", 30);
        TestValueObject vo2 = new("John", 30);

        vo1.Equals(vo2).Should().BeTrue();
        (vo1 == vo2).Should().BeTrue();
    }

    [Fact]
    public void Equals_DifferentValues_ReturnsFalse()
    {
        TestValueObject vo1 = new("John", 30);
        TestValueObject vo2 = new("Jane", 30);

        vo1.Equals(vo2).Should().BeFalse();
        (vo1 != vo2).Should().BeTrue();
    }

    [Fact]
    public void Equals_Null_ReturnsFalse()
    {
        TestValueObject vo1 = new("John", 30);

        // Intentionally testing null equality behavior
#pragma warning disable CA1508 // Avoid dead conditional code
        vo1.Equals(null).Should().BeFalse();
#pragma warning restore CA1508
    }

    [Fact]
    public void Equals_DifferentType_ReturnsFalse()
    {
        TestValueObject vo1 = new("John", 30);
        OtherTestValueObject vo2 = new("John", 30);

        vo1.Equals(vo2).Should().BeFalse();
    }

    [Fact]
    public void Equals_SameReference_ReturnsTrue()
    {
        TestValueObject vo1 = new("John", 30);
        TestValueObject vo2 = vo1;

        vo1.Equals(vo2).Should().BeTrue();
    }

    [Fact]
    public void GetHashCode_SameValues_ReturnsSameHash()
    {
        TestValueObject vo1 = new("John", 30);
        TestValueObject vo2 = new("John", 30);

        vo1.GetHashCode().Should().Be(vo2.GetHashCode());
    }

    [Fact]
    public void GetHashCode_DifferentValues_ReturnsDifferentHash()
    {
        TestValueObject vo1 = new("John", 30);
        TestValueObject vo2 = new("Jane", 25);

        // Note: Hash collisions are possible but unlikely for simple cases
        vo1.GetHashCode().Should().NotBe(vo2.GetHashCode());
    }

    [Fact]
    public void Equals_WithNullComponent_HandlesCorrectly()
    {
        TestValueObject vo1 = new(null, 30);
        TestValueObject vo2 = new(null, 30);

        vo1.Equals(vo2).Should().BeTrue();
    }

    [Fact]
    public void Equals_OneNullComponent_ReturnsFalse()
    {
        TestValueObject vo1 = new("John", 30);
        TestValueObject vo2 = new(null, 30);

        vo1.Equals(vo2).Should().BeFalse();
    }

    [Fact]
    public void OperatorEquals_BothNull_ReturnsTrue()
    {
        TestValueObject? vo1 = null;
        TestValueObject? vo2 = null;

        // Intentionally testing null-null equality
#pragma warning disable CA1508 // Avoid dead conditional code
        (vo1 == vo2).Should().BeTrue();
#pragma warning restore CA1508
    }

    [Fact]
    public void OperatorEquals_OneNull_ReturnsFalse()
    {
        TestValueObject vo1 = new("John", 30);
        TestValueObject? vo2 = null;

        // Intentionally testing value-null equality
#pragma warning disable CA1508 // Avoid dead conditional code
        (vo1 == vo2).Should().BeFalse();
        (vo2 == vo1).Should().BeFalse();
#pragma warning restore CA1508
    }

    // Test value objects for testing
    private sealed class TestValueObject(string? name, int age) : ValueObject
    {
        protected override IEnumerable<object?> GetEqualityComponents()
        {
            yield return name;
            yield return age;
        }
    }

    private sealed class OtherTestValueObject(string? name, int age) : ValueObject
    {
        protected override IEnumerable<object?> GetEqualityComponents()
        {
            yield return name;
            yield return age;
        }
    }
}
