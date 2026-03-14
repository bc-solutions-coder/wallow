namespace Foundry.Inquiries.Application.DTOs;

public sealed record InquiryCommentDto(
    Guid Id,
    Guid InquiryId,
    string AuthorId,
    string AuthorName,
    string Content,
    bool IsInternal,
    DateTimeOffset CreatedAt);
