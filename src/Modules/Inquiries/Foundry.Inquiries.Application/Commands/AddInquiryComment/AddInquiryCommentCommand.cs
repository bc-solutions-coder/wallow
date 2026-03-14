using Foundry.Inquiries.Domain.Identity;

namespace Foundry.Inquiries.Application.Commands.AddInquiryComment;

public sealed record AddInquiryCommentCommand(
    InquiryId InquiryId,
    string AuthorId,
    string AuthorName,
    string Content,
    bool IsInternal,
    Guid TenantId);
