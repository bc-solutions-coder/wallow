namespace Foundry.Inquiries.Api.Contracts;

public sealed record InquiryCommentResponse(
    Guid Id,
    Guid InquiryId,
    string AuthorId,
    string AuthorName,
    string Content,
    bool IsInternal,
    DateTime CreatedAt);
