using Wallow.Identity.Domain.Identity;

namespace Wallow.Identity.Tests.Domain;

public class InvitationIdTests
{
    [Fact]
    public void Create_WithGuid_SetsValue()
    {
        Guid guid = Guid.NewGuid();

        InvitationId id = InvitationId.Create(guid);

        id.Value.Should().Be(guid);
    }

    [Fact]
    public void New_GeneratesNonEmptyGuid()
    {
        InvitationId id = InvitationId.New();

        id.Value.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void Equals_SameValue_ReturnsTrue()
    {
        Guid guid = Guid.NewGuid();
        InvitationId id1 = InvitationId.Create(guid);
        InvitationId id2 = InvitationId.Create(guid);

        id1.Should().Be(id2);
        (id1 == id2).Should().BeTrue();
    }

    [Fact]
    public void Equals_DifferentValue_ReturnsFalse()
    {
        InvitationId id1 = InvitationId.New();
        InvitationId id2 = InvitationId.New();

        id1.Should().NotBe(id2);
        (id1 != id2).Should().BeTrue();
    }
}

public class MembershipRequestIdTests
{
    [Fact]
    public void Create_WithGuid_SetsValue()
    {
        Guid guid = Guid.NewGuid();

        MembershipRequestId id = MembershipRequestId.Create(guid);

        id.Value.Should().Be(guid);
    }

    [Fact]
    public void New_GeneratesNonEmptyGuid()
    {
        MembershipRequestId id = MembershipRequestId.New();

        id.Value.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void Equals_SameValue_ReturnsTrue()
    {
        Guid guid = Guid.NewGuid();
        MembershipRequestId id1 = MembershipRequestId.Create(guid);
        MembershipRequestId id2 = MembershipRequestId.Create(guid);

        id1.Should().Be(id2);
        (id1 == id2).Should().BeTrue();
    }

    [Fact]
    public void Equals_DifferentValue_ReturnsFalse()
    {
        MembershipRequestId id1 = MembershipRequestId.New();
        MembershipRequestId id2 = MembershipRequestId.New();

        id1.Should().NotBe(id2);
        (id1 != id2).Should().BeTrue();
    }
}

public class OrganizationBrandingIdTests
{
    [Fact]
    public void Create_WithGuid_SetsValue()
    {
        Guid guid = Guid.NewGuid();

        OrganizationBrandingId id = OrganizationBrandingId.Create(guid);

        id.Value.Should().Be(guid);
    }

    [Fact]
    public void New_GeneratesNonEmptyGuid()
    {
        OrganizationBrandingId id = OrganizationBrandingId.New();

        id.Value.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void Equals_SameValue_ReturnsTrue()
    {
        Guid guid = Guid.NewGuid();
        OrganizationBrandingId id1 = OrganizationBrandingId.Create(guid);
        OrganizationBrandingId id2 = OrganizationBrandingId.Create(guid);

        id1.Should().Be(id2);
        (id1 == id2).Should().BeTrue();
    }

    [Fact]
    public void Equals_DifferentValue_ReturnsFalse()
    {
        OrganizationBrandingId id1 = OrganizationBrandingId.New();
        OrganizationBrandingId id2 = OrganizationBrandingId.New();

        id1.Should().NotBe(id2);
        (id1 != id2).Should().BeTrue();
    }
}

public class OrganizationDomainIdTests
{
    [Fact]
    public void Create_WithGuid_SetsValue()
    {
        Guid guid = Guid.NewGuid();

        OrganizationDomainId id = OrganizationDomainId.Create(guid);

        id.Value.Should().Be(guid);
    }

    [Fact]
    public void New_GeneratesNonEmptyGuid()
    {
        OrganizationDomainId id = OrganizationDomainId.New();

        id.Value.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void Equals_SameValue_ReturnsTrue()
    {
        Guid guid = Guid.NewGuid();
        OrganizationDomainId id1 = OrganizationDomainId.Create(guid);
        OrganizationDomainId id2 = OrganizationDomainId.Create(guid);

        id1.Should().Be(id2);
        (id1 == id2).Should().BeTrue();
    }

    [Fact]
    public void Equals_DifferentValue_ReturnsFalse()
    {
        OrganizationDomainId id1 = OrganizationDomainId.New();
        OrganizationDomainId id2 = OrganizationDomainId.New();

        id1.Should().NotBe(id2);
        (id1 != id2).Should().BeTrue();
    }
}

public class OrganizationSettingsIdTests
{
    [Fact]
    public void Create_WithGuid_SetsValue()
    {
        Guid guid = Guid.NewGuid();

        OrganizationSettingsId id = OrganizationSettingsId.Create(guid);

        id.Value.Should().Be(guid);
    }

    [Fact]
    public void New_GeneratesNonEmptyGuid()
    {
        OrganizationSettingsId id = OrganizationSettingsId.New();

        id.Value.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void Equals_SameValue_ReturnsTrue()
    {
        Guid guid = Guid.NewGuid();
        OrganizationSettingsId id1 = OrganizationSettingsId.Create(guid);
        OrganizationSettingsId id2 = OrganizationSettingsId.Create(guid);

        id1.Should().Be(id2);
        (id1 == id2).Should().BeTrue();
    }

    [Fact]
    public void Equals_DifferentValue_ReturnsFalse()
    {
        OrganizationSettingsId id1 = OrganizationSettingsId.New();
        OrganizationSettingsId id2 = OrganizationSettingsId.New();

        id1.Should().NotBe(id2);
        (id1 != id2).Should().BeTrue();
    }
}
