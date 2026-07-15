using Microsoft.Extensions.Logging;
using OpenIddict.Abstractions;

namespace Wallow.Identity.Infrastructure.Jobs;

public sealed partial class OpenIddictTokenPruningJob(
    IOpenIddictTokenManager tokenManager,
    IOpenIddictAuthorizationManager authorizationManager,
    ILogger<OpenIddictTokenPruningJob> logger)
{
    public async Task ExecuteAsync()
    {
        LogPruningStarted(logger);

        try
        {
            DateTimeOffset threshold = DateTimeOffset.UtcNow;

            await tokenManager.PruneAsync(threshold);
            await authorizationManager.PruneAsync(threshold);

            LogPruningCompleted(logger);
        }
        catch (Exception ex)
        {
            LogPruningFailed(logger, ex);
            throw;
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Starting OpenIddict token and authorization pruning")]
    private static partial void LogPruningStarted(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "OpenIddict token and authorization pruning completed")]
    private static partial void LogPruningCompleted(ILogger logger);

    [LoggerMessage(Level = LogLevel.Error, Message = "OpenIddict token and authorization pruning failed")]
    private static partial void LogPruningFailed(ILogger logger, Exception ex);
}
