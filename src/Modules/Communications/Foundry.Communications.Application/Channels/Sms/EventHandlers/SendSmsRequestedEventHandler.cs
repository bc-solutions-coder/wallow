using Foundry.Communications.Application.Channels.Sms.Commands.SendSms;
using Foundry.Shared.Contracts.Communications.Sms.Events;
using Foundry.Shared.Kernel.Results;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace Foundry.Communications.Application.Channels.Sms.EventHandlers;

public static partial class SendSmsRequestedEventHandler
{
    public static async Task HandleAsync(
        SendSmsRequestedEvent @event,
        IMessageBus bus,
        ILogger<SendSmsRequestedEvent> logger,
        CancellationToken cancellationToken = default)
    {
        LogProcessingSendSms(logger, @event.SourceModule ?? "Unknown", @event.To);

        try
        {
            SendSmsCommand command = new(@event.To, @event.Body);
            await bus.InvokeAsync<Result>(command, cancellationToken);

            LogSmsSent(logger, @event.To);
        }
        catch (Exception ex)
        {
            LogSmsFailed(logger, ex, @event.To, @event.EventId);
            throw;
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Processing SendSmsRequestedEvent from {SourceModule} to {To}")]
    private static partial void LogProcessingSendSms(ILogger logger, string sourceModule, string to);

    [LoggerMessage(Level = LogLevel.Information, Message = "SMS sent successfully to {To}")]
    private static partial void LogSmsSent(ILogger logger, string to);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to send SMS to {To} for event {EventId}")]
    private static partial void LogSmsFailed(ILogger logger, Exception ex, string to, Guid eventId);
}
