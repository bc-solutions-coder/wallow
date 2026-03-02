using Foundry.Communications.Application.Channels.Email.DTOs;
using Foundry.Communications.Application.Channels.Email.Interfaces;
using Foundry.Communications.Application.Channels.Email.Mappings;
using Foundry.Communications.Domain.Channels.Email.Entities;
using Foundry.Communications.Domain.Channels.Email.ValueObjects;
using Foundry.Shared.Contracts.Communications.Email;
using Foundry.Shared.Kernel.Results;

namespace Foundry.Communications.Application.Channels.Email.Commands.SendEmail;

public sealed class SendEmailHandler(
    IEmailMessageRepository emailMessageRepository,
    IEmailService emailService,
    TimeProvider timeProvider)
{
    public async Task<Result<EmailDto>> Handle(
        SendEmailCommand command,
        CancellationToken cancellationToken)
    {
        EmailAddress to = EmailAddress.Create(command.To);
        EmailAddress? from = string.IsNullOrWhiteSpace(command.From)
            ? null
            : EmailAddress.Create(command.From);
        EmailContent content = EmailContent.Create(command.Subject, command.Body);

        EmailMessage emailMessage = EmailMessage.Create(to, from, content, timeProvider);
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
