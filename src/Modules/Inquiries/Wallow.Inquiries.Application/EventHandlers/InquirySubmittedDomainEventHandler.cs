using Wallow.Inquiries.Application.Interfaces;
using Wallow.Inquiries.Domain.Entities;
using Wallow.Inquiries.Domain.Events;
using Wallow.Inquiries.Domain.Identity;
using Microsoft.Extensions.Configuration;
using Wolverine;

namespace Wallow.Inquiries.Application.EventHandlers;

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

        string adminEmail = configuration["Inquiries:AdminEmail"] ?? "admin@wallow.local";

        await bus.PublishAsync(new Shared.Contracts.Inquiries.Events.InquirySubmittedEvent
        {
            InquiryId = domainEvent.InquiryId,
            Name = domainEvent.Name,
            Email = domainEvent.Email,
            Company = domainEvent.Company,
            Phone = domainEvent.Phone,
            ProjectType = domainEvent.ProjectType,
            Message = domainEvent.Message,
            SubmittedAt = inquiry?.CreatedAt ?? DateTime.UtcNow,
            AdminEmail = adminEmail
        });
    }
}
