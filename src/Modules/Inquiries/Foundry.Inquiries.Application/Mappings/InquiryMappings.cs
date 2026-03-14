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
            Phone: inquiry.Phone,
            Company: inquiry.Company,
            SubmitterId: inquiry.SubmitterId,
            ProjectType: inquiry.ProjectType,
            BudgetRange: inquiry.BudgetRange,
            Timeline: inquiry.Timeline,
            Message: inquiry.Message,
            Status: inquiry.Status.ToString(),
            SubmitterIpAddress: inquiry.SubmitterIpAddress,
            CreatedAt: inquiry.CreatedAt);
    }

    public static InquiryCommentDto ToCommentDto(this InquiryComment comment)
    {
        return new InquiryCommentDto(
            Id: comment.Id.Value,
            InquiryId: comment.InquiryId.Value,
            AuthorId: comment.AuthorId,
            AuthorName: comment.AuthorName,
            Content: comment.Content,
            IsInternal: comment.IsInternal,
            CreatedAt: comment.CreatedAt);
    }
}
