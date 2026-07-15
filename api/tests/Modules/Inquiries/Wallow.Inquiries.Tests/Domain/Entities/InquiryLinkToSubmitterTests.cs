using Wallow.Inquiries.Domain.Entities;

namespace Wallow.Inquiries.Tests.Domain.Entities;

public class InquiryLinkToSubmitterTests
{
    private static Inquiry CreateNewInquiry(string? submitterId = null)
    {
        Inquiry inquiry = Inquiry.Create("Test", "test@example.com", "555-0100", null, submitterId, "Type", "Budget", "Timeline", "Message", "1.1.1.1", TimeProvider.System);
        inquiry.ClearDomainEvents();
        return inquiry;
    }

    [Fact]
    public void LinkToSubmitter_WithNoExistingSubmitter_SetsSubmitterId()
    {
        Inquiry inquiry = CreateNewInquiry();
        string submitterId = Guid.NewGuid().ToString();

        inquiry.LinkToSubmitter(submitterId, TimeProvider.System);

        inquiry.SubmitterId.Should().Be(submitterId);
    }

    [Fact]
    public void LinkToSubmitter_SetsUpdatedTimestamp()
    {
        Inquiry inquiry = CreateNewInquiry();
        DateTime before = DateTime.UtcNow;

        inquiry.LinkToSubmitter(Guid.NewGuid().ToString(), TimeProvider.System);

        inquiry.UpdatedAt.Should().NotBeNull();
        inquiry.UpdatedAt.Should().BeOnOrAfter(before);
    }

    [Fact]
    public void LinkToSubmitter_WhenAlreadyLinked_DoesNotOverwrite()
    {
        string originalSubmitterId = Guid.NewGuid().ToString();
        Inquiry inquiry = CreateNewInquiry(originalSubmitterId);

        inquiry.LinkToSubmitter(Guid.NewGuid().ToString(), TimeProvider.System);

        inquiry.SubmitterId.Should().Be(originalSubmitterId);
    }

    [Fact]
    public void LinkToSubmitter_WithNullOrWhitespace_ThrowsArgumentException()
    {
        Inquiry inquiry = CreateNewInquiry();

        Action act = () => inquiry.LinkToSubmitter("  ", TimeProvider.System);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void LinkToSubmitter_WithNull_ThrowsArgumentException()
    {
        Inquiry inquiry = CreateNewInquiry();

        Action act = () => inquiry.LinkToSubmitter(null!, TimeProvider.System);

        act.Should().Throw<ArgumentException>();
    }
}
