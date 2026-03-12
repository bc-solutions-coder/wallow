using Foundry.Notifications.Application.Channels.Sms.Interfaces;
using Foundry.Notifications.Application.Preferences.Interfaces;
using Foundry.Notifications.Domain.Channels.Sms.Entities;
using Foundry.Notifications.Domain.Channels.Sms.ValueObjects;
using Foundry.Notifications.Domain.Preferences;
using Foundry.Shared.Kernel.MultiTenancy;
using Foundry.Shared.Kernel.Results;

namespace Foundry.Notifications.Application.Channels.Sms.Commands.SendSms;

public sealed class SendSmsHandler(
    ISmsMessageRepository smsMessageRepository,
    ISmsProvider smsProvider,
    ITenantContext tenantContext,
    TimeProvider timeProvider,
    INotificationPreferenceChecker preferenceChecker)
{
    public async Task<Result> Handle(
        SendSmsCommand command,
        CancellationToken cancellationToken)
    {
        if (command.UserId is not null && command.NotificationType is not null)
        {
            bool isEnabled = await preferenceChecker.IsChannelEnabledAsync(
                command.UserId.Value, ChannelType.Sms, command.NotificationType, cancellationToken);

            if (!isEnabled)
            {
                return Result.Success();
            }
        }

        PhoneNumber to = PhoneNumber.Create(command.To);
        PhoneNumber? from = command.From is not null ? PhoneNumber.Create(command.From) : null;

        SmsMessage smsMessage = SmsMessage.Create(tenantContext.TenantId, to, from, command.Body, timeProvider);
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
                smsMessage.MarkAsSent(timeProvider);
            }
            else
            {
                smsMessage.MarkAsFailed(deliveryResult.ErrorMessage ?? "Unknown error", timeProvider);
            }
        }
        catch (Exception ex)
        {
            smsMessage.MarkAsFailed(ex.Message, timeProvider);
        }

        await smsMessageRepository.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
