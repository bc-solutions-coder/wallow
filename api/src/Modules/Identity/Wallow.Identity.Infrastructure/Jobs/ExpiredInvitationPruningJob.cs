using Microsoft.Extensions.Logging;
using Wallow.Identity.Application.Interfaces;

namespace Wallow.Identity.Infrastructure.Jobs;

public sealed partial class ExpiredInvitationPruningJob(
    IInvitationService invitationService,
    ILogger<ExpiredInvitationPruningJob> logger)
{
    public async Task ExecuteAsync()
    {
        LogPruningStarted(logger);

        try
        {
            await invitationService.CleanupExpiredAsync();

            LogPruningCompleted(logger);
        }
        catch (Exception ex)
        {
            LogPruningFailed(logger, ex);
            throw;
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Starting expired invitation pruning")]
    private static partial void LogPruningStarted(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Expired invitation pruning completed")]
    private static partial void LogPruningCompleted(ILogger logger);

    [LoggerMessage(Level = LogLevel.Error, Message = "Expired invitation pruning failed")]
    private static partial void LogPruningFailed(ILogger logger, Exception ex);
}
