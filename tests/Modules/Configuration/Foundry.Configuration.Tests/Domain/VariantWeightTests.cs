using Foundry.Configuration.Domain.ValueObjects;

namespace Foundry.Configuration.Tests.Domain;

public class VariantWeightConstructionTests
{
    [Fact]
    public void Constructor_WithValidData_SetsProperties()
    {
        VariantWeight variant = new("control", 50);

        variant.Name.Should().Be("control");
        variant.Weight.Should().Be(50);
    }

    [Fact]
    public void Constructor_WithZeroWeight_Succeeds()
    {
        VariantWeight variant = new("disabled", 0);

        variant.Weight.Should().Be(0);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithEmptyName_ThrowsArgumentException(string name)
    {
        Action act = () => _ = new VariantWeight(name, 50);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*Variant name is required*");
    }

    [Fact]
    public void Constructor_WithNullName_ThrowsArgumentException()
    {
        Action act = () => _ = new VariantWeight(null!, 50);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*Variant name is required*");
    }

    [Fact]
    public void Constructor_WithNegativeWeight_ThrowsArgumentOutOfRangeException()
    {
        Action act = () => _ = new VariantWeight("test", -1);

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*Weight cannot be negative*");
    }
}

public class VariantWeightEqualityTests
{
    [Fact]
    public void Equals_WithSameNameAndWeight_ReturnsTrue()
    {
        VariantWeight a = new("control", 50);
        VariantWeight b = new("control", 50);

        a.Should().Be(b);
    }

    [Fact]
    public void Equals_WithDifferentName_ReturnsFalse()
    {
        VariantWeight a = new("control", 50);
        VariantWeight b = new("treatment", 50);

        a.Should().NotBe(b);
    }

    [Fact]
    public void Equals_WithDifferentWeight_ReturnsFalse()
    {
        VariantWeight a = new("control", 50);
        VariantWeight b = new("control", 75);

        a.Should().NotBe(b);
    }

    [Fact]
    public void GetHashCode_WithSameValues_ReturnsSameHash()
    {
        VariantWeight a = new("control", 50);
        VariantWeight b = new("control", 50);

        a.GetHashCode().Should().Be(b.GetHashCode());
    }
}
