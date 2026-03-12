using Foundry.Inquiries.Application.DTOs;
using Foundry.Inquiries.Application.Mappings;
using Foundry.Inquiries.Domain.Entities;
using Foundry.Inquiries.Domain.Enums;

namespace Foundry.Inquiries.Tests.Application.Mappings;

public class InquiryMappingsTests
{
    [Fact]
    public void ToDto_MapsAllFieldsCorrectly()
    {
        Inquiry inquiry = Inquiry.Create(
            "Jane Doe", "jane@example.com", "Acme Corp", "Mobile App",
            "$50k", "6 months", "Build us a mobile app.", "10.0.0.1",
            TimeProvider.System);

        InquiryDto dto = inquiry.ToDto();

        dto.Id.Should().Be(inquiry.Id.Value);
        dto.Name.Should().Be("Jane Doe");
        dto.Email.Should().Be("jane@example.com");
        dto.Company.Should().Be("Acme Corp");
        dto.ProjectType.Should().Be("Mobile App");
        dto.BudgetRange.Should().Be("$50k");
        dto.Timeline.Should().Be("6 months");
        dto.Message.Should().Be("Build us a mobile app.");
        dto.SubmitterIpAddress.Should().Be("10.0.0.1");
        dto.Status.Should().Be("New");
        dto.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void ToDto_WithNullCompany_MapsCompanyAsNull()
    {
        Inquiry inquiry = Inquiry.Create(
            "John", "john@example.com", null, "Web App",
            "$10k", "3 months", "Help.", "1.1.1.1",
            TimeProvider.System);

        InquiryDto dto = inquiry.ToDto();

        dto.Company.Should().BeNull();
    }

    [Fact]
    public void ToDto_AfterStatusTransition_ReflectsNewStatus()
    {
        Inquiry inquiry = Inquiry.Create(
            "Alice", "alice@example.com", null, "Web App",
            "$10k", "3 months", "Help.", "1.1.1.1",
            TimeProvider.System);
        inquiry.TransitionTo(InquiryStatus.Reviewed, TimeProvider.System);

        InquiryDto dto = inquiry.ToDto();

        dto.Status.Should().Be("Reviewed");
    }

    [Fact]
    public void ToDto_StatusToString_MatchesEnumName()
    {
        Inquiry inquiry = Inquiry.Create(
            "Bob", "bob@example.com", null, "Web App",
            "$10k", "3 months", "Help.", "1.1.1.1",
            TimeProvider.System);
        inquiry.TransitionTo(InquiryStatus.Reviewed, TimeProvider.System);
        inquiry.TransitionTo(InquiryStatus.Contacted, TimeProvider.System);
        inquiry.TransitionTo(InquiryStatus.Closed, TimeProvider.System);

        InquiryDto dto = inquiry.ToDto();

        dto.Status.Should().Be("Closed");
    }
}
