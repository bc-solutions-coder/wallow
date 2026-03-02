using Foundry.Communications.Application.Channels.Email.Interfaces;
using Foundry.Communications.Domain.Channels.Email.Entities;
using Foundry.Shared.Contracts.Communications.Email;
using Microsoft.Extensions.Logging;

namespace Foundry.Communications.Infrastructure.Jobs;

public sealed partial class RetryFailedEmailsJob
{
    private readonly IEmailMessageRepository _emailMessageRepository;
    private readonly IEmailService _emailService;
    private readonly ILogger<RetryFailedEmailsJob> _logger;

    public RetryFailedEmailsJob(
        IEmailMessageRepository emailMessageRepository,
        IEmailService emailService,
        ILogger<RetryFailedEmailsJob> logger)
    {
        _emailMessageRepository = emailMessageRepository;
        _emailService = emailService;
        _logger = logger;
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        LogRetryJobStarted(_logger);
        int retriedCount = 0;

        try
        {
            IReadOnlyList<EmailMessage> failedMessages = await _emailMessageRepository
                .GetFailedRetryableAsync(maxRetries: 3, limit: 100, cancellationToken);

            foreach (EmailMessage message in failedMessages)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                try
                {
                    message.ResetForRetry();

                    await _emailService.SendAsync(
                        message.To.Value,
                        message.From?.Value,
                        message.Content.Subject,
                        message.Content.Body,
                        cancellationToken);

                    message.MarkAsSent();
                    retriedCount++;
                    LogEmailRetrySucceeded(_logger, message.Id.Value);
                }
                catch (Exception ex)
                {
                    message.MarkAsFailed(ex.Message);
                    LogEmailRetryFailed(_logger, ex, message.Id.Value);
                }
            }

            await _emailMessageRepository.SaveChangesAsync(cancellationToken);
            LogRetryJobCompleted(_logger, retriedCount, failedMessages.Count);
        }
        catch (Exception ex)
        {
            LogRetryJobError(_logger, ex);
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
