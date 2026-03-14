using Foundry.Inquiries.Domain.Entities;
using Foundry.Inquiries.Domain.Enums;
using Foundry.Inquiries.Domain.Events;

namespace Foundry.Inquiries.Tests.Domain.Entities;

public class InquiryCreateTests
{
    [Fact]
    public void Create_WithValidData_ReturnsInquiryWithNewStatus()
    {
        string name = "John Doe";
        string email = "john@example.com";
        string company = "Acme Corp";
        string projectType = "Web Application";
        string budgetRange = "$10k - $50k";
        string timeline = "3 months";
        string message = "Looking for a partner to build our platform.";
        string ip = "192.168.1.1";

        Inquiry inquiry = Inquiry.Create(name, email, "555-0100", company, null, projectType, budgetRange, timeline, message, ip, TimeProvider.System);

        inquiry.Name.Should().Be(name);
        inquiry.Email.Should().Be(email);
        inquiry.Company.Should().Be(company);
        inquiry.ProjectType.Should().Be(projectType);
        inquiry.BudgetRange.Should().Be(budgetRange);
        inquiry.Timeline.Should().Be(timeline);
        inquiry.Message.Should().Be(message);
        inquiry.SubmitterIpAddress.Should().Be(ip);
        inquiry.Status.Should().Be(InquiryStatus.New);
    }

    [Fact]
    public void Create_RaisesInquirySubmittedDomainEvent()
    {
        string name = "Jane Smith";
        string email = "jane@example.com";
        string? company = null;
        string projectType = "Mobile App";
        string budgetRange = "$5k - $10k";
        string timeline = "6 months";
        string message = "Need a mobile app for our business.";
        string ip = "10.0.0.1";

        Inquiry inquiry = Inquiry.Create(name, email, "555-0100", company, null, projectType, budgetRange, timeline, message, ip, TimeProvider.System);

        inquiry.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<InquirySubmittedDomainEvent>()
            .Which.Should().Match<InquirySubmittedDomainEvent>(e =>
                e.InquiryId == inquiry.Id.Value &&
                e.Name == name &&
                e.Email == email &&
                e.Company == company &&
                e.ProjectType == projectType &&
                e.BudgetRange == budgetRange &&
                e.Timeline == timeline &&
                e.Message == message);
    }

    [Fact]
    public void Create_SetsCreatedTimestamp()
    {
        DateTime before = DateTime.UtcNow;

        Inquiry inquiry = Inquiry.Create("Test", "test@example.com", "555-0100", null, null, "Type", "Budget", "Timeline", "Message", "1.1.1.1", TimeProvider.System);

        inquiry.CreatedAt.Should().BeOnOrAfter(before);
        inquiry.CreatedAt.Should().BeOnOrBefore(DateTime.UtcNow);
    }
}
