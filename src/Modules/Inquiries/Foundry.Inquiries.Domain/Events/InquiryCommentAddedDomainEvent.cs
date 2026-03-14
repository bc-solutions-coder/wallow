using Foundry.Shared.Kernel.Domain;

namespace Foundry.Inquiries.Domain.Events;

public sealed record InquiryCommentAddedDomainEvent(
    Guid InquiryCommentId,
    Guid InquiryId,
    Guid TenantId,
    string AuthorId,
    bool IsInternal) : DomainEvent;
