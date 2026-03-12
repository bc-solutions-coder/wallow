using Foundry.Inquiries.Application.Interfaces;
using Foundry.Inquiries.Domain.Entities;
using Foundry.Inquiries.Domain.Events;
using Foundry.Inquiries.Domain.Identity;
using Wolverine;

namespace Foundry.Inquiries.Application.EventHandlers;

public static class InquiryStatusChangedDomainEventHandler
{
    public static async Task HandleAsync(
        InquiryStatusChangedDomainEvent domainEvent,
        IInquiryRepository repository,
        IMessageBus bus,
        CancellationToken ct)
    {
        Inquiry? inquiry = await repository.GetByIdAsync(
            InquiryId.Create(domainEvent.InquiryId), ct);

        await bus.PublishAsync(new Shared.Contracts.Inquiries.Events.InquiryStatusChangedEvent
        {
            InquiryId = domainEvent.InquiryId,
            OldStatus = domainEvent.OldStatus,
            NewStatus = domainEvent.NewStatus,
            ChangedAt = inquiry?.UpdatedAt ?? DateTime.UtcNow,
            SubmitterEmail = inquiry?.Email ?? string.Empty
        });
    }
}
