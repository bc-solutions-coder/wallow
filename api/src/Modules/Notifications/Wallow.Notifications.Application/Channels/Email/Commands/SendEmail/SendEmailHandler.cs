using Wallow.Notifications.Application.Channels.Email.DTOs;
using Wallow.Notifications.Application.Channels.Email.Interfaces;
using Wallow.Notifications.Application.Channels.Email.Mappings;
using Wallow.Notifications.Application.Preferences.Interfaces;
using Wallow.Notifications.Domain.Channels.Email.Entities;
using Wallow.Notifications.Domain.Channels.Email.ValueObjects;
using Wallow.Notifications.Domain.Preferences;
using Wallow.Shared.Contracts.Communications.Email;
using Wallow.Shared.Kernel.MultiTenancy;
using Wallow.Shared.Kernel.Results;

namespace Wallow.Notifications.Application.Channels.Email.Commands.SendEmail;

public sealed class SendEmailHandler(
    IEmailMessageRepository emailMessageRepository,
    IEmailService emailService,
    ITenantContext tenantContext,
    TimeProvider timeProvider,
    INotificationPreferenceChecker preferenceChecker)
{
    public async Task<Result<EmailDto>> Handle(
        SendEmailCommand command,
        CancellationToken cancellationToken)
    {
        if (command.UserId is not null && command.NotificationType is not null)
        {
            bool isEnabled = await preferenceChecker.IsChannelEnabledAsync(
                command.UserId.Value, ChannelType.Email, command.NotificationType, cancellationToken);

            if (!isEnabled)
            {
                return Result.Success<EmailDto>(default!);
            }
        }

        EmailAddress to = EmailAddress.Create(command.To);
        EmailAddress? from = string.IsNullOrWhiteSpace(command.From)
            ? null
            : EmailAddress.Create(command.From);
        EmailContent content = EmailContent.Create(command.Subject, command.Body);

        EmailMessage emailMessage = EmailMessage.Create(tenantContext.TenantId, to, from, content, timeProvider);
        emailMessageRepository.Add(emailMessage);
        await emailMessageRepository.SaveChangesAsync(cancellationToken);

        try
        {
            await emailService.SendAsync(
                command.To,
                command.From,
                command.Subject,
                command.Body,
                cancellationToken);

            emailMessage.MarkAsSent(timeProvider);
        }
        catch (Exception ex)
        {
            emailMessage.MarkAsFailed(ex.Message, timeProvider);
        }

        await emailMessageRepository.SaveChangesAsync(cancellationToken);

        return Result.Success(emailMessage.ToDto());
    }
}
