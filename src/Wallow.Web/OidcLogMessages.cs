namespace Wallow.Web;

internal static partial class OidcLogMessages
{
    [LoggerMessage(Level = LogLevel.Error, Message = "OIDC authentication failed")]
    public static partial void AuthenticationFailed(ILogger logger, Exception? exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "OIDC remote failure")]
    public static partial void RemoteFailure(ILogger logger, Exception? exception);
}
