using Wallow.Notifications.Application.Channels.Email.Interfaces;
using Wallow.Notifications.Domain.Channels.Email.Entities;
using Wallow.Shared.Contracts.Communications.Email;
using Microsoft.Extensions.Logging;

namespace Wallow.Notifications.Infrastructure.Jobs;

public sealed partial class RetryFailedEmailsJob(
    IEmailMessageRepository emailMessageRepository,
    IEmailService emailService,
    TimeProvider timeProvider,
    ILogger<RetryFailedEmailsJob> logger)
{

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        LogRetryJobStarted(logger);
        int retriedCount = 0;

        try
        {
            IReadOnlyList<EmailMessage> failedMessages = await emailMessageRepository
                .GetFailedRetryableAsync(maxRetries: 3, limit: 100, cancellationToken);

            foreach (EmailMessage message in failedMessages)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                try
                {
                    message.ResetForRetry(timeProvider);

                    await emailService.SendAsync(
                        message.To.Value,
                        message.From?.Value,
                        message.Content.Subject,
                        message.Content.Body,
                        cancellationToken);

                    message.MarkAsSent(timeProvider);
                    retriedCount++;
                    LogEmailRetrySucceeded(logger, message.Id.Value);
                }
                catch (Exception ex)
                {
                    message.MarkAsFailed(ex.Message, timeProvider);
                    LogEmailRetryFailed(logger, ex, message.Id.Value);
                }
            }

            await emailMessageRepository.SaveChangesAsync(cancellationToken);
            LogRetryJobCompleted(logger, retriedCount, failedMessages.Count);
        }
        catch (Exception ex)
        {
            LogRetryJobError(logger, ex);
            throw;
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Starting retry failed emails job")]
    private static partial void LogRetryJobStarted(ILogger logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Email {EmailId} retry succeeded")]
    private static partial void LogEmailRetrySucceeded(ILogger logger, Guid emailId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Email {EmailId} retry failed")]
    private static partial void LogEmailRetryFailed(ILogger logger, Exception ex, Guid emailId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Retry failed emails job completed. Retried {RetriedCount}/{TotalCount} emails")]
    private static partial void LogRetryJobCompleted(ILogger logger, int retriedCount, int totalCount);

    [LoggerMessage(Level = LogLevel.Error, Message = "Error during retry failed emails job")]
    private static partial void LogRetryJobError(ILogger logger, Exception ex);
}
