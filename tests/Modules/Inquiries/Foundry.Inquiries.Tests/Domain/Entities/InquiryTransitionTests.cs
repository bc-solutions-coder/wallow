using Foundry.Inquiries.Domain.Entities;
using Foundry.Inquiries.Domain.Enums;
using Foundry.Inquiries.Domain.Events;
using Foundry.Inquiries.Domain.Exceptions;

namespace Foundry.Inquiries.Tests.Domain.Entities;

public class InquiryTransitionTests
{
    private static Inquiry CreateNewInquiry()
    {
        Inquiry inquiry = Inquiry.Create("Test", "test@example.com", "555-0100", null, null, "Type", "Budget", "Timeline", "Message", "1.1.1.1", TimeProvider.System);
        inquiry.ClearDomainEvents();
        return inquiry;
    }

    [Fact]
    public void TransitionTo_FromNewToReviewed_Succeeds()
    {
        Inquiry inquiry = CreateNewInquiry();

        inquiry.TransitionTo(InquiryStatus.Reviewed, TimeProvider.System);

        inquiry.Status.Should().Be(InquiryStatus.Reviewed);
    }

    [Fact]
    public void TransitionTo_FromReviewedToContacted_Succeeds()
    {
        Inquiry inquiry = CreateNewInquiry();
        inquiry.TransitionTo(InquiryStatus.Reviewed, TimeProvider.System);
        inquiry.ClearDomainEvents();

        inquiry.TransitionTo(InquiryStatus.Contacted, TimeProvider.System);

        inquiry.Status.Should().Be(InquiryStatus.Contacted);
    }

    [Fact]
    public void TransitionTo_FromContactedToClosed_Succeeds()
    {
        Inquiry inquiry = CreateNewInquiry();
        inquiry.TransitionTo(InquiryStatus.Reviewed, TimeProvider.System);
        inquiry.TransitionTo(InquiryStatus.Contacted, TimeProvider.System);
        inquiry.ClearDomainEvents();

        inquiry.TransitionTo(InquiryStatus.Closed, TimeProvider.System);

        inquiry.Status.Should().Be(InquiryStatus.Closed);
    }

    [Fact]
    public void TransitionTo_RaisesStatusChangedDomainEvent()
    {
        Inquiry inquiry = CreateNewInquiry();
        InquiryStatus oldStatus = inquiry.Status;

        inquiry.TransitionTo(InquiryStatus.Reviewed, TimeProvider.System);

        inquiry.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<InquiryStatusChangedDomainEvent>()
            .Which.Should().Match<InquiryStatusChangedDomainEvent>(e =>
                e.InquiryId == inquiry.Id.Value &&
                e.OldStatus == oldStatus.ToString() &&
                e.NewStatus == InquiryStatus.Reviewed.ToString());
    }

    [Fact]
    public void TransitionTo_SetsUpdatedTimestamp()
    {
        Inquiry inquiry = CreateNewInquiry();
        DateTime before = DateTime.UtcNow;

        inquiry.TransitionTo(InquiryStatus.Reviewed, TimeProvider.System);

        inquiry.UpdatedAt.Should().NotBeNull();
        inquiry.UpdatedAt.Should().BeOnOrAfter(before);
    }

    [Theory]
    [InlineData(InquiryStatus.New, InquiryStatus.Closed)]
    [InlineData(InquiryStatus.New, InquiryStatus.Contacted)]
    [InlineData(InquiryStatus.Reviewed, InquiryStatus.New)]
    [InlineData(InquiryStatus.Reviewed, InquiryStatus.Closed)]
    [InlineData(InquiryStatus.Contacted, InquiryStatus.New)]
    [InlineData(InquiryStatus.Contacted, InquiryStatus.Reviewed)]
    public void TransitionTo_InvalidTransition_ThrowsException(InquiryStatus from, InquiryStatus to)
    {
        Inquiry inquiry = CreateNewInquiry();

        // Advance to the 'from' state
        if (from == InquiryStatus.Reviewed)
        {
            inquiry.TransitionTo(InquiryStatus.Reviewed, TimeProvider.System);
        }
        else if (from == InquiryStatus.Contacted)
        {
            inquiry.TransitionTo(InquiryStatus.Reviewed, TimeProvider.System);
            inquiry.TransitionTo(InquiryStatus.Contacted, TimeProvider.System);
        }

        inquiry.ClearDomainEvents();

        Action act = () => inquiry.TransitionTo(to, TimeProvider.System);

        act.Should().Throw<InvalidInquiryStatusTransitionException>();
    }
}
