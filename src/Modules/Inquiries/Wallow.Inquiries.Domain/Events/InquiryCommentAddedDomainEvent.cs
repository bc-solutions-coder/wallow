using Wallow.Shared.Kernel.Domain;

namespace Wallow.Inquiries.Domain.Events;

public sealed record InquiryCommentAddedDomainEvent(
    Guid InquiryCommentId,
    Guid InquiryId,
    Guid TenantId,
    string AuthorId,
    bool IsInternal,
    string CommentContent) : DomainEvent;
