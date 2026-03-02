using Foundry.Communications.Application.Channels.Sms.Interfaces;
using Foundry.Communications.Domain.Channels.Sms.Entities;
using Foundry.Communications.Domain.Channels.Sms.ValueObjects;
using Foundry.Shared.Kernel.MultiTenancy;
using Foundry.Shared.Kernel.Results;

namespace Foundry.Communications.Application.Channels.Sms.Commands.SendSms;

public sealed class SendSmsHandler(
    ISmsMessageRepository smsMessageRepository,
    ISmsProvider smsProvider,
    ITenantContext tenantContext)
{
    public async Task<Result> Handle(
        SendSmsCommand command,
        CancellationToken cancellationToken)
    {
        PhoneNumber to = PhoneNumber.Create(command.To);

        SmsMessage smsMessage = SmsMessage.Create(tenantContext.TenantId, to, command.Body);
        smsMessageRepository.Add(smsMessage);
        await smsMessageRepository.SaveChangesAsync(cancellationToken);

        try
        {
            SmsDeliveryResult deliveryResult = await smsProvider.SendAsync(
                command.To,
                command.Body,
                cancellationToken);

            if (deliveryResult.Success)
            {
                smsMessage.MarkAsSent();
            }
            else
            {
                smsMessage.MarkAsFailed(deliveryResult.ErrorMessage ?? "Unknown error");
            }
        }
        catch (Exception ex)
        {
            smsMessage.MarkAsFailed(ex.Message);
        }

        await smsMessageRepository.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
