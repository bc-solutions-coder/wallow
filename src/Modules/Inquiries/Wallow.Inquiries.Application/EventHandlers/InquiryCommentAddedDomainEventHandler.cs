using Wallow.Inquiries.Application.Interfaces;
using Wallow.Inquiries.Domain.Entities;
using Wallow.Inquiries.Domain.Events;
using Wallow.Inquiries.Domain.Identity;
using Wolverine;

namespace Wallow.Inquiries.Application.EventHandlers;

public static class InquiryCommentAddedDomainEventHandler
{
    public static async Task HandleAsync(
        InquiryCommentAddedDomainEvent domainEvent,
        IInquiryCommentRepository commentRepository,
        IInquiryRepository inquiryRepository,
        IMessageBus bus,
        CancellationToken ct)
    {
        InquiryComment? comment = await commentRepository.GetByIdAsync(
            InquiryCommentId.Create(domainEvent.InquiryCommentId), ct);

        Inquiry? inquiry = await inquiryRepository.GetByIdAsync(
            InquiryId.Create(domainEvent.InquiryId), ct);

        Guid? submitterUserId = inquiry?.SubmitterId is not null
            && Guid.TryParse(inquiry.SubmitterId, out Guid parsed)
                ? parsed
                : null;

        await bus.PublishAsync(new Shared.Contracts.Inquiries.Events.InquiryCommentAddedEvent
        {
            InquiryCommentId = domainEvent.InquiryCommentId,
            InquiryId = domainEvent.InquiryId,
            TenantId = domainEvent.TenantId,
            AuthorId = domainEvent.AuthorId,
            AuthorName = comment?.AuthorName ?? string.Empty,
            IsInternal = domainEvent.IsInternal,
            SubmitterEmail = inquiry?.Email ?? string.Empty,
            SubmitterName = inquiry?.Name ?? string.Empty,
            SubmitterUserId = submitterUserId,
            InquirySubject = inquiry?.ProjectType ?? string.Empty,
            CommentContent = domainEvent.CommentContent
        });
    }
}
