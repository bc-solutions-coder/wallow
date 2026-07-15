using Wallow.Identity.Domain.Entities;
using Wallow.Shared.Kernel.Domain;

namespace Wallow.Identity.Tests.Domain;

public class OrganizationMemberTests
{
    [Fact]
    public void Constructor_WithValidData_SetsProperties()
    {
        Guid userId = Guid.NewGuid();

        OrganizationMember member = new(userId, "admin");

        member.UserId.Should().Be(userId);
        member.Role.Should().Be("admin");
    }

    [Fact]
    public void Constructor_WithEmptyUserId_ThrowsBusinessRuleException()
    {
        Action act = () => _ = new OrganizationMember(Guid.Empty, "member");

        act.Should().Throw<BusinessRuleException>()
            .WithMessage("*User ID*");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Constructor_WithBlankRole_ThrowsBusinessRuleException(string? role)
    {
        Action act = () => _ = new OrganizationMember(Guid.NewGuid(), role!);

        act.Should().Throw<BusinessRuleException>()
            .WithMessage("*role*");
    }

    [Fact]
    public void Equality_SameUserIdAndRole_AreEqual()
    {
        Guid userId = Guid.NewGuid();
        OrganizationMember member1 = new(userId, "admin");
        OrganizationMember member2 = new(userId, "admin");

        member1.Should().Be(member2);
    }

    [Fact]
    public void Equality_DifferentUserId_AreNotEqual()
    {
        OrganizationMember member1 = new(Guid.NewGuid(), "admin");
        OrganizationMember member2 = new(Guid.NewGuid(), "admin");

        member1.Should().NotBe(member2);
    }

    [Fact]
    public void Equality_DifferentRole_AreNotEqual()
    {
        Guid userId = Guid.NewGuid();
        OrganizationMember member1 = new(userId, "admin");
        OrganizationMember member2 = new(userId, "member");

        member1.Should().NotBe(member2);
    }

    [Fact]
    public void GetHashCode_SameUserIdAndRole_ReturnsSameHash()
    {
        Guid userId = Guid.NewGuid();
        OrganizationMember member1 = new(userId, "admin");
        OrganizationMember member2 = new(userId, "admin");

        member1.GetHashCode().Should().Be(member2.GetHashCode());
    }

    [Fact]
    public void GetHashCode_DifferentValues_ReturnsDifferentHash()
    {
        Guid userId = Guid.NewGuid();
        OrganizationMember member1 = new(userId, "admin");
        OrganizationMember member2 = new(userId, "member");

        member1.GetHashCode().Should().NotBe(member2.GetHashCode());
    }

    [Fact]
    public void EqualityOperator_SameValues_ReturnsTrue()
    {
        Guid userId = Guid.NewGuid();
        OrganizationMember member1 = new(userId, "admin");
        OrganizationMember member2 = new(userId, "admin");

        (member1 == member2).Should().BeTrue();
    }

    [Fact]
    public void InequalityOperator_DifferentValues_ReturnsTrue()
    {
        OrganizationMember member1 = new(Guid.NewGuid(), "admin");
        OrganizationMember member2 = new(Guid.NewGuid(), "admin");

        (member1 != member2).Should().BeTrue();
    }

    [Fact]
    public void Equals_WithDifferentType_ReturnsFalse()
    {
        OrganizationMember member = new(Guid.NewGuid(), "admin");

        member.Equals("not a value object").Should().BeFalse();
    }
}
