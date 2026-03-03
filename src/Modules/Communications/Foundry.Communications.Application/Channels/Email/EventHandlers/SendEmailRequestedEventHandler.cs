using Foundry.Communications.Application.Channels.Email.Interfaces;
using Foundry.Communications.Domain.Channels.Email.Entities;
using Foundry.Communications.Domain.Channels.Email.ValueObjects;
using Foundry.Shared.Contracts.Communications.Email;
using Foundry.Shared.Contracts.Communications.Email.Events;
using Foundry.Shared.Kernel.Identity;
using Microsoft.Extensions.Logging;

namespace Foundry.Communications.Application.Channels.Email.EventHandlers;

public static partial class SendEmailRequestedEventHandler
{
    public static async Task HandleAsync(
        SendEmailRequestedEvent @event,
        IEmailService emailService,
        IEmailMessageRepository emailMessageRepository,
        TimeProvider timeProvider,
        ILogger<SendEmailRequestedEvent> logger,
        CancellationToken cancellationToken = default)
    {
        LogProcessingSendEmail(logger, @event.SourceModule ?? "Unknown", @event.To);

        EmailAddress to = EmailAddress.Create(@event.To);
        EmailAddress? from = string.IsNullOrWhiteSpace(@event.From)
            ? null
            : EmailAddress.Create(@event.From);
        EmailContent content = EmailContent.Create(@event.Subject, @event.Body);
        TenantId tenantId = new(@event.TenantId);

        EmailMessage emailMessage = EmailMessage.Create(tenantId, to, from, content, timeProvider);
        emailMessageRepository.Add(emailMessage);
        await emailMessageRepository.SaveChangesAsync(cancellationToken);

        try
        {
            await emailService.SendAsync(
                @event.To,
                @event.From,
                @event.Subject,
                @event.Body,
                cancellationToken);

            emailMessage.MarkAsSent(timeProvider);
            LogEmailSent(logger, @event.To);
        }
        catch (Exception ex)
        {
            emailMessage.MarkAsFailed(ex.Message, timeProvider);
            LogEmailFailed(logger, ex, @event.To, @event.EventId);
            throw;
        }

        await emailMessageRepository.SaveChangesAsync(cancellationToken);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Processing SendEmailRequestedEvent from {SourceModule} to {To}")]
    private static partial void LogProcessingSendEmail(ILogger logger, string sourceModule, string to);

    [LoggerMessage(Level = LogLevel.Information, Message = "Email sent successfully to {To}")]
    private static partial void LogEmailSent(ILogger logger, string to);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to send email to {To} for event {EventId}")]
    private static partial void LogEmailFailed(ILogger logger, Exception ex, string to, Guid eventId);
}
