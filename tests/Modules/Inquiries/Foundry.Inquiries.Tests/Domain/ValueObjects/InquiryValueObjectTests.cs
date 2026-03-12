using Foundry.Inquiries.Domain.Identity;

namespace Foundry.Inquiries.Tests.Domain.ValueObjects;

public class InquiryValueObjectTests
{
    [Fact]
    public void InquiryId_New_ReturnsUniqueIds()
    {
        InquiryId first = InquiryId.New();
        InquiryId second = InquiryId.New();

        first.Should().NotBe(second);
        first.Value.Should().NotBe(Guid.Empty);
        second.Value.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void InquiryId_Create_WithGuid_ReturnsIdWithSameValue()
    {
        Guid guid = Guid.NewGuid();

        InquiryId id = InquiryId.Create(guid);

        id.Value.Should().Be(guid);
    }

    [Fact]
    public void InquiryId_Equality_SameGuid_AreEqual()
    {
        Guid guid = Guid.NewGuid();

        InquiryId first = InquiryId.Create(guid);
        InquiryId second = InquiryId.Create(guid);

        first.Should().Be(second);
        (first == second).Should().BeTrue();
    }

    [Fact]
    public void InquiryId_Equality_DifferentGuid_AreNotEqual()
    {
        InquiryId first = InquiryId.New();
        InquiryId second = InquiryId.New();

        first.Should().NotBe(second);
        (first != second).Should().BeTrue();
    }

    [Fact]
    public void InquiryId_Create_WithEmptyGuid_ReturnsIdWithEmptyValue()
    {
        InquiryId id = InquiryId.Create(Guid.Empty);

        id.Value.Should().Be(Guid.Empty);
    }
}
