using Microsoft.Extensions.Logging;
using Wallow.Notifications.Application.Channels.Push.Interfaces;
using Wallow.Notifications.Domain.Channels.Push;
using Wallow.Notifications.Domain.Channels.Push.Entities;
using Wolverine;

namespace Wallow.Notifications.Application.Channels.Push.Commands.DeliverPush;

public sealed partial class DeliverPushHandler(
    IPushProviderFactory pushProviderFactory,
    IPushMessageRepository pushMessageRepository,
    IDeviceRegistrationRepository deviceRegistrationRepository,
    TimeProvider timeProvider,
    ILogger<DeliverPushHandler> logger,
    IMessageBus messageBus)
{
    public async Task Handle(
        DeliverPushCommand command,
        CancellationToken cancellationToken)
    {
        PushMessage? pushMessage = await pushMessageRepository.GetByIdAsync(
            command.PushMessageId, cancellationToken);

        if (pushMessage is null)
        {
            LogPushMessageNotFound(logger, command.PushMessageId.Value);
            return;
        }

        DeviceRegistration? device = await deviceRegistrationRepository.GetByIdAsync(command.DeviceRegistrationId, cancellationToken);
        if (device is null || !device.IsActive || device.UserId != pushMessage.RecipientId)
        {
            LogDeviceUnavailable(logger, command.DeviceRegistrationId.Value);
            return;
        }

        TimeSpan? retryDelay = null;
        try
        {
            IPushProvider provider = await pushProviderFactory.GetProviderAsync(device);

            PushDeliveryResult result = await provider.SendAsync(
                pushMessage, device.Token, cancellationToken);

            if (result.Success)
            {
                pushMessage.MarkAccepted(timeProvider);
                LogPushDelivered(logger, command.PushMessageId.Value);
            }
            else
            {
                if (result.SubscriptionExpired)
                {
                    device.Deactivate();
                    await deviceRegistrationRepository.SaveDeactivationAsync(device, cancellationToken);
                }
                pushMessage.MarkFailed(result.ErrorMessage ?? "Unknown error", timeProvider);
                if (result.Retryable && command.Attempt < 2)
                {
                    retryDelay = result.RetryAfter ?? TimeSpan.FromSeconds(30 * (command.Attempt + 1));
                }
                LogPushFailed(logger, command.PushMessageId.Value, result.ErrorMessage ?? "Unknown error");
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            pushMessage.MarkFailed(ex.Message, timeProvider);
            LogPushException(logger, ex, command.PushMessageId.Value);
            throw;
        }
        finally
        {
            pushMessageRepository.Update(pushMessage);
            await pushMessageRepository.SaveChangesAsync(cancellationToken);
        }
        if (retryDelay is TimeSpan delay)
        {
            await messageBus.PublishAsync(command with { Attempt = command.Attempt + 1 }, new DeliveryOptions { ScheduleDelay = delay });
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Skipping push for unavailable device registration {DeviceRegistrationId}")]
    private static partial void LogDeviceUnavailable(ILogger logger, Guid deviceRegistrationId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Push message {PushMessageId} not found")]
    private static partial void LogPushMessageNotFound(ILogger logger, Guid pushMessageId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Push message {PushMessageId} accepted by push provider")]
    private static partial void LogPushDelivered(ILogger logger, Guid pushMessageId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Push message {PushMessageId} delivery failed: {Reason}")]
    private static partial void LogPushFailed(ILogger logger, Guid pushMessageId, string reason);

    [LoggerMessage(Level = LogLevel.Error, Message = "Push message {PushMessageId} delivery threw exception")]
    private static partial void LogPushException(ILogger logger, Exception ex, Guid pushMessageId);
}
