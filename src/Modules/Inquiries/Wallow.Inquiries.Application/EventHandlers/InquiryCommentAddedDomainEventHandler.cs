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
        IInquiryCommentRepository repository,
        IMessageBus bus,
        CancellationToken ct)
    {
        InquiryComment? comment = await repository.GetByIdAsync(
            InquiryCommentId.Create(domainEvent.InquiryCommentId), ct);

        await bus.PublishAsync(new Shared.Contracts.Inquiries.Events.InquiryCommentAddedEvent
        {
            InquiryCommentId = domainEvent.InquiryCommentId,
            InquiryId = domainEvent.InquiryId,
            TenantId = domainEvent.TenantId,
            AuthorId = domainEvent.AuthorId,
            AuthorName = comment?.AuthorName ?? string.Empty,
            IsInternal = domainEvent.IsInternal
        });
    }
}
