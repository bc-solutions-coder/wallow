using Wallow.Inquiries.Application.Interfaces;
using Wallow.Inquiries.Domain.Entities;
using Wallow.Inquiries.Domain.Events;
using Wallow.Inquiries.Domain.Identity;
using Wolverine;

namespace Wallow.Inquiries.Application.EventHandlers;

public static class InquiryStatusChangedDomainEventHandler
{
    public static async Task HandleAsync(
        InquiryStatusChangedDomainEvent domainEvent,
        IInquiryRepository repository,
        IMessageBus bus,
        TimeProvider timeProvider,
        CancellationToken ct)
    {
        Inquiry? inquiry = await repository.GetByIdAsync(
            InquiryId.Create(domainEvent.InquiryId), ct);

        await bus.PublishAsync(new Shared.Contracts.Inquiries.Events.InquiryStatusChangedEvent
        {
            InquiryId = domainEvent.InquiryId,
            OldStatus = domainEvent.OldStatus,
            NewStatus = domainEvent.NewStatus,
            ChangedAt = inquiry?.UpdatedAt ?? timeProvider.GetUtcNow().UtcDateTime,
            SubmitterEmail = inquiry?.Email ?? string.Empty
        });
    }
}
