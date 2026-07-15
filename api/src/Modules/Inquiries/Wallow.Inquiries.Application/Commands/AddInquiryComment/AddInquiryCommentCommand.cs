using Wallow.Inquiries.Domain.Identity;

namespace Wallow.Inquiries.Application.Commands.AddInquiryComment;

public sealed record AddInquiryCommentCommand(
    InquiryId InquiryId,
    string AuthorId,
    string AuthorName,
    string Content,
    bool IsInternal);
