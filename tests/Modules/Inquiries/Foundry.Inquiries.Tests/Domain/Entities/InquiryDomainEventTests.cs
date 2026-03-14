using Foundry.Inquiries.Domain.Entities;
using Foundry.Inquiries.Domain.Enums;
using Foundry.Inquiries.Domain.Events;

namespace Foundry.Inquiries.Tests.Domain.Entities;

public class InquiryDomainEventTests
{
    private static Inquiry CreateNewInquiry()
    {
        Inquiry inquiry = Inquiry.Create("Test", "test@example.com", "555-0100", null, null, "Type", "Budget", "Timeline", "Message", "1.1.1.1", TimeProvider.System);
        inquiry.ClearDomainEvents();
        return inquiry;
    }

    [Fact]
    public void TransitionTo_ReviewedToContacted_RaisesStatusChangedEvent()
    {
        Inquiry inquiry = CreateNewInquiry();
        inquiry.TransitionTo(InquiryStatus.Reviewed, TimeProvider.System);
        inquiry.ClearDomainEvents();

        inquiry.TransitionTo(InquiryStatus.Contacted, TimeProvider.System);

        inquiry.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<InquiryStatusChangedDomainEvent>()
            .Which.Should().Match<InquiryStatusChangedDomainEvent>(e =>
                e.InquiryId == inquiry.Id.Value &&
                e.OldStatus == InquiryStatus.Reviewed.ToString() &&
                e.NewStatus == InquiryStatus.Contacted.ToString());
    }

    [Fact]
    public void TransitionTo_ContactedToClosed_RaisesStatusChangedEvent()
    {
        Inquiry inquiry = CreateNewInquiry();
        inquiry.TransitionTo(InquiryStatus.Reviewed, TimeProvider.System);
        inquiry.TransitionTo(InquiryStatus.Contacted, TimeProvider.System);
        inquiry.ClearDomainEvents();

        inquiry.TransitionTo(InquiryStatus.Closed, TimeProvider.System);

        inquiry.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<InquiryStatusChangedDomainEvent>()
            .Which.Should().Match<InquiryStatusChangedDomainEvent>(e =>
                e.InquiryId == inquiry.Id.Value &&
                e.OldStatus == InquiryStatus.Contacted.ToString() &&
                e.NewStatus == InquiryStatus.Closed.ToString());
    }

    [Fact]
    public void TransitionTo_FullLifecycle_RaisesThreeStatusChangedEvents()
    {
        Inquiry inquiry = CreateNewInquiry();

        inquiry.TransitionTo(InquiryStatus.Reviewed, TimeProvider.System);
        inquiry.TransitionTo(InquiryStatus.Contacted, TimeProvider.System);
        inquiry.TransitionTo(InquiryStatus.Closed, TimeProvider.System);

        inquiry.DomainEvents.Should().HaveCount(3);
        inquiry.DomainEvents.Should().AllBeOfType<InquiryStatusChangedDomainEvent>();
    }

    [Fact]
    public void Create_WithNullCompany_EventContainsNullCompany()
    {
        Inquiry inquiry = Inquiry.Create("Test", "test@example.com", "555-0100", null, null, "Type", "Budget", "Timeline", "Message", "1.1.1.1", TimeProvider.System);

        inquiry.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<InquirySubmittedDomainEvent>()
            .Which.Company.Should().BeNull();
    }

    [Fact]
    public void Create_WithCompany_EventContainsCompany()
    {
        string company = "Acme Corp";

        Inquiry inquiry = Inquiry.Create("Test", "test@example.com", "555-0100", company, null, "Type", "Budget", "Timeline", "Message", "1.1.1.1", TimeProvider.System);

        inquiry.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<InquirySubmittedDomainEvent>()
            .Which.Company.Should().Be(company);
    }
}
