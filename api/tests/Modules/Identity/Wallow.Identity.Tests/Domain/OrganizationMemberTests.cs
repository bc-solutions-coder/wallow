using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Domain.Enums;
using Wallow.Shared.Kernel.Domain;

namespace Wallow.Identity.Tests.Domain;

public class OrganizationMemberTests
{
    [Fact]
    public void Constructor_WithValidData_SetsProperties()
    {
        Guid userId = Guid.NewGuid();

        OrganizationMember member = new(userId, OrgMemberRole.Admin);

        member.UserId.Should().Be(userId);
        member.Role.Should().Be(OrgMemberRole.Admin);
    }

    [Fact]
    public void Constructor_WithEmptyUserId_ThrowsBusinessRuleException()
    {
        Action act = () => _ = new OrganizationMember(Guid.Empty, OrgMemberRole.Member);

        act.Should().Throw<BusinessRuleException>()
            .WithMessage("*User ID*");
    }

    [Fact]
    public void Equality_SameUserIdAndRole_AreEqual()
    {
        Guid userId = Guid.NewGuid();
        OrganizationMember member1 = new(userId, OrgMemberRole.Admin);
        OrganizationMember member2 = new(userId, OrgMemberRole.Admin);

        member1.Should().Be(member2);
    }

    [Fact]
    public void Equality_DifferentUserId_AreNotEqual()
    {
        OrganizationMember member1 = new(Guid.NewGuid(), OrgMemberRole.Admin);
        OrganizationMember member2 = new(Guid.NewGuid(), OrgMemberRole.Admin);

        member1.Should().NotBe(member2);
    }

    [Fact]
    public void Equality_DifferentRole_AreNotEqual()
    {
        Guid userId = Guid.NewGuid();
        OrganizationMember member1 = new(userId, OrgMemberRole.Admin);
        OrganizationMember member2 = new(userId, OrgMemberRole.Member);

        member1.Should().NotBe(member2);
    }

    [Fact]
    public void GetHashCode_SameUserIdAndRole_ReturnsSameHash()
    {
        Guid userId = Guid.NewGuid();
        OrganizationMember member1 = new(userId, OrgMemberRole.Admin);
        OrganizationMember member2 = new(userId, OrgMemberRole.Admin);

        member1.GetHashCode().Should().Be(member2.GetHashCode());
    }

    [Fact]
    public void GetHashCode_DifferentValues_ReturnsDifferentHash()
    {
        Guid userId = Guid.NewGuid();
        OrganizationMember member1 = new(userId, OrgMemberRole.Admin);
        OrganizationMember member2 = new(userId, OrgMemberRole.Member);

        member1.GetHashCode().Should().NotBe(member2.GetHashCode());
    }

    [Fact]
    public void EqualityOperator_SameValues_ReturnsTrue()
    {
        Guid userId = Guid.NewGuid();
        OrganizationMember member1 = new(userId, OrgMemberRole.Admin);
        OrganizationMember member2 = new(userId, OrgMemberRole.Admin);

        (member1 == member2).Should().BeTrue();
    }

    [Fact]
    public void InequalityOperator_DifferentValues_ReturnsTrue()
    {
        OrganizationMember member1 = new(Guid.NewGuid(), OrgMemberRole.Admin);
        OrganizationMember member2 = new(Guid.NewGuid(), OrgMemberRole.Admin);

        (member1 != member2).Should().BeTrue();
    }

    [Fact]
    public void Equals_WithDifferentType_ReturnsFalse()
    {
        OrganizationMember member = new(Guid.NewGuid(), OrgMemberRole.Admin);

        member.Equals("not a value object").Should().BeFalse();
    }
}
