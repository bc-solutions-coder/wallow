using Microsoft.Extensions.Logging;
using Wallow.Notifications.Application.Channels.Push.Interfaces;
using Wallow.Notifications.Domain.Channels.Push.Entities;

namespace Wallow.Notifications.Application.Channels.Push.Commands.DeliverPush;

public sealed partial class DeliverPushHandler(
    IPushProviderFactory pushProviderFactory,
    IPushMessageRepository pushMessageRepository,
    TimeProvider timeProvider,
    ILogger<DeliverPushHandler> logger)
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

        try
        {
            IPushProvider provider = await pushProviderFactory.GetProviderAsync(command.Platform);

            PushDeliveryResult result = await provider.SendAsync(
                pushMessage, command.Token, cancellationToken);

            if (result.Success)
            {
                pushMessage.MarkDelivered(timeProvider);
                LogPushDelivered(logger, command.PushMessageId.Value);
            }
            else
            {
                pushMessage.MarkFailed(result.ErrorMessage ?? "Unknown error", timeProvider);
                LogPushFailed(logger, command.PushMessageId.Value, result.ErrorMessage ?? "Unknown error");
            }
        }
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
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Push message {PushMessageId} not found")]
    private static partial void LogPushMessageNotFound(ILogger logger, Guid pushMessageId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Push message {PushMessageId} delivered successfully")]
    private static partial void LogPushDelivered(ILogger logger, Guid pushMessageId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Push message {PushMessageId} delivery failed: {Reason}")]
    private static partial void LogPushFailed(ILogger logger, Guid pushMessageId, string reason);

    [LoggerMessage(Level = LogLevel.Error, Message = "Push message {PushMessageId} delivery threw exception")]
    private static partial void LogPushException(ILogger logger, Exception ex, Guid pushMessageId);
}
