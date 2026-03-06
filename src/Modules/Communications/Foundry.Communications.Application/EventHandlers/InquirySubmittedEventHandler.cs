using Foundry.Shared.Contracts.Communications.Email.Events;
using Foundry.Shared.Contracts.Inquiries.Events;
using Microsoft.Extensions.Configuration;
using Wolverine;

namespace Foundry.Communications.Application.EventHandlers;

public static class InquirySubmittedEventHandler
{
    public static async Task HandleAsync(
        InquirySubmittedEvent evt,
        IMessageBus bus,
        IConfiguration config)
    {
        string adminEmail = config["Inquiries:AdminEmail"] ?? "admin@foundry.dev";

        SendEmailRequestedEvent adminNotification = new()
        {
            TenantId = Guid.Empty,
            To = adminEmail,
            Subject = "New Inquiry Received",
            Body = $"""
                A new inquiry has been submitted.

                Name: {evt.Name}
                Email: {evt.Email}
                Company: {evt.Company ?? "N/A"}
                Message: {evt.Message}
                """,
            SourceModule = "Inquiries"
        };

        SendEmailRequestedEvent confirmation = new()
        {
            TenantId = Guid.Empty,
            To = evt.Email,
            Subject = "We received your inquiry",
            Body = $"""
                Hi {evt.Name},

                Thank you for reaching out. We have received your inquiry and will get back to you shortly.

                Best regards,
                The Foundry Team
                """,
            SourceModule = "Inquiries"
        };

        await bus.PublishAsync(adminNotification);
        await bus.PublishAsync(confirmation);
    }
}
