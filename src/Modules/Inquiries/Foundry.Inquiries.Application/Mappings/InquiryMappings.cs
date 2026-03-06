using Foundry.Inquiries.Application.DTOs;
using Foundry.Inquiries.Domain.Entities;

namespace Foundry.Inquiries.Application.Mappings;

public static class InquiryMappings
{
    public static InquiryDto ToDto(this Inquiry inquiry)
    {
        return new InquiryDto(
            Id: inquiry.Id.Value,
            Name: inquiry.Name,
            Email: inquiry.Email,
            Company: inquiry.Company,
            ProjectType: inquiry.ProjectType,
            BudgetRange: inquiry.BudgetRange,
            Timeline: inquiry.Timeline,
            Message: inquiry.Message,
            Status: inquiry.Status.ToString(),
            SubmitterIpAddress: inquiry.SubmitterIpAddress,
            CreatedAt: inquiry.CreatedAt);
    }
}
