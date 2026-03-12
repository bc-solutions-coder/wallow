using Foundry.Inquiries.Application.Interfaces;
using Foundry.Inquiries.Domain.Entities;
using Foundry.Inquiries.Domain.Events;
using Foundry.Inquiries.Domain.Identity;
using Microsoft.Extensions.Configuration;
using Wolverine;

namespace Foundry.Inquiries.Application.EventHandlers;

public static class InquirySubmittedDomainEventHandler
{
    public static async Task HandleAsync(
        InquirySubmittedDomainEvent domainEvent,
        IInquiryRepository repository,
        IConfiguration configuration,
        IMessageBus bus,
        CancellationToken ct)
    {
        Inquiry? inquiry = await repository.GetByIdAsync(
            InquiryId.Create(domainEvent.InquiryId), ct);

        string adminEmail = configuration["Inquiries:AdminEmail"] ?? "admin@foundry.local";

        await bus.PublishAsync(new Shared.Contracts.Inquiries.Events.InquirySubmittedEvent
        {
            InquiryId = domainEvent.InquiryId,
            Name = domainEvent.Name,
            Email = domainEvent.Email,
            Company = domainEvent.Company,
            Subject = domainEvent.ProjectType,
            Message = domainEvent.Message,
            SubmittedAt = inquiry?.CreatedAt ?? DateTime.UtcNow,
            AdminEmail = adminEmail
        });
    }
}
