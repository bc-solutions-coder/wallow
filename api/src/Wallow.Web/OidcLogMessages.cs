namespace Wallow.Web;

internal static partial class OidcLogMessages
{
    [LoggerMessage(Level = LogLevel.Error, Message = "OIDC authentication failed")]
    public static partial void AuthenticationFailed(ILogger logger, Exception? exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "OIDC remote failure")]
    public static partial void RemoteFailure(ILogger logger, Exception? exception);

    [LoggerMessage(Level = LogLevel.Debug, Message = "OIDC authorization code received for {Scheme}")]
    public static partial void OnAuthorizationCodeReceived(ILogger logger, string scheme);

    [LoggerMessage(Level = LogLevel.Debug, Message = "OIDC token validated for {Subject} from {Issuer}")]
    public static partial void OnTokenValidated(ILogger logger, string subject, string issuer);

    [LoggerMessage(Level = LogLevel.Debug, Message = "OIDC token response received for {Scheme}")]
    public static partial void OnTokenResponseReceived(ILogger logger, string scheme);

    [LoggerMessage(Level = LogLevel.Debug, Message = "OIDC redirecting to identity provider at {RedirectUri}")]
    public static partial void OnRedirectToIdentityProvider(ILogger logger, string redirectUri);
}
