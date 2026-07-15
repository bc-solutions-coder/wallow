using Wallow.Inquiries.Domain.Entities;
using Wallow.Inquiries.Domain.Enums;
using Wallow.Inquiries.Domain.Exceptions;

namespace Wallow.Inquiries.Tests.Domain.Entities;

public class InquiryStatusTransitionTests
{
    private static Inquiry CreateNewInquiry()
    {
        Inquiry inquiry = Inquiry.Create("Test", "test@example.com", "555-0100", null, null, "Type", "Budget", "Timeline", "Message", "1.1.1.1", TimeProvider.System);
        inquiry.ClearDomainEvents();
        return inquiry;
    }

    private static Inquiry CreateInquiryAtStatus(InquiryStatus target)
    {
        Inquiry inquiry = CreateNewInquiry();

        if (target >= InquiryStatus.Reviewed)
        {
            inquiry.TransitionTo(InquiryStatus.Reviewed, TimeProvider.System);
        }

        if (target >= InquiryStatus.Contacted)
        {
            inquiry.TransitionTo(InquiryStatus.Contacted, TimeProvider.System);
        }

        if (target >= InquiryStatus.Closed)
        {
            inquiry.TransitionTo(InquiryStatus.Closed, TimeProvider.System);
        }

        inquiry.ClearDomainEvents();
        return inquiry;
    }

    [Theory]
    [InlineData(InquiryStatus.Closed, InquiryStatus.New)]
    [InlineData(InquiryStatus.Closed, InquiryStatus.Reviewed)]
    [InlineData(InquiryStatus.Closed, InquiryStatus.Contacted)]
    [InlineData(InquiryStatus.Closed, InquiryStatus.Closed)]
    public void TransitionTo_FromClosedToAnyStatus_ThrowsInvalidTransitionException(InquiryStatus from, InquiryStatus to)
    {
        Inquiry inquiry = CreateInquiryAtStatus(from);

        Action act = () => inquiry.TransitionTo(to, TimeProvider.System);

        act.Should().Throw<InvalidInquiryStatusTransitionException>();
    }

    [Theory]
    [InlineData(InquiryStatus.New)]
    [InlineData(InquiryStatus.Reviewed)]
    [InlineData(InquiryStatus.Contacted)]
    public void TransitionTo_ToSameStatus_ThrowsInvalidTransitionException(InquiryStatus status)
    {
        Inquiry inquiry = CreateInquiryAtStatus(status);

        Action act = () => inquiry.TransitionTo(status, TimeProvider.System);

        act.Should().Throw<InvalidInquiryStatusTransitionException>();
    }

    [Fact]
    public void TransitionTo_FromNewToReviewed_DoesNotThrow()
    {
        Inquiry inquiry = CreateNewInquiry();

        Action act = () => inquiry.TransitionTo(InquiryStatus.Reviewed, TimeProvider.System);

        act.Should().NotThrow();
    }

    [Fact]
    public void TransitionTo_InvalidTransition_PreservesOriginalStatus()
    {
        Inquiry inquiry = CreateNewInquiry();
        InquiryStatus originalStatus = inquiry.Status;

        Action act = () => inquiry.TransitionTo(InquiryStatus.Closed, TimeProvider.System);

        act.Should().Throw<InvalidInquiryStatusTransitionException>();
        inquiry.Status.Should().Be(originalStatus);
    }

    [Fact]
    public void TransitionTo_InvalidTransition_DoesNotRaiseDomainEvent()
    {
        Inquiry inquiry = CreateNewInquiry();

        Action act = () => inquiry.TransitionTo(InquiryStatus.Closed, TimeProvider.System);

        act.Should().Throw<InvalidInquiryStatusTransitionException>();
        inquiry.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void TransitionTo_FullLifecycle_ReachesClosedStatus()
    {
        Inquiry inquiry = CreateNewInquiry();

        inquiry.TransitionTo(InquiryStatus.Reviewed, TimeProvider.System);
        inquiry.TransitionTo(InquiryStatus.Contacted, TimeProvider.System);
        inquiry.TransitionTo(InquiryStatus.Closed, TimeProvider.System);

        inquiry.Status.Should().Be(InquiryStatus.Closed);
    }
}
